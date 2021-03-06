﻿/*
* Demoder.PlanetMapViewer
* Copyright (C) 2012, 2013 Demoder (demoder@demoder.me)
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Demoder.Common;
using Demoder.PlanetMapViewer.DataClasses;
using Demoder.PlanetMapViewer.Forms;
using Demoder.PlanetMapViewer.Helpers;
using Demoder.PlanetMapViewer.Xna;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Demoder.PlanetMapViewer.PmvApi;

namespace Demoder.PlanetMapViewer.Xna
{
    public class TileDisplay : GraphicsDeviceControl
    {
        /// <summary>
        /// Delay (in milliseconds) between frames.
        /// </summary>
        public static int FrameFrequency = 33;
        /// <summary>
        /// Used to limit FPS of the tileDisplay.
        /// </summary>
        private Stopwatch timeSinceLastDraw = Stopwatch.StartNew();

        private float mouseScrollSensitivity = 1;

        /// <summary>
        /// Determines wether or not the user is panning the map
        /// </summary>
        private bool isPanningMap = false;
        private Vector2 mousePosition = Vector2.Zero;

        public event EventHandler OnDraw;
        public event EventHandler OnInitialize;

        private object drawLocker = new Object();

        private PlayerInfoKey lastActiveID = null;

        public TileDisplay()
        {
        }

        private void GraphicsDeviceDeviceLost(object sender, EventArgs e)
        {
            Program.WriteLog("GraphicsDevice: Lost");
        }

        #region Constructor / Initialization
        protected override void Initialize()
        {
            if (this.OnInitialize != null)
            {
                this.OnInitialize(this, null);
            }

            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            //this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.ResizeRedraw, true);

            API.UiElements.TileDisplay = this;

            try
            {
                API.ContentManager = new ContentManager(Services, "Content");
                API.Content.Fonts.Load();
                API.Content.Loaded = true;
                ThreadPool.QueueUserWorkItem(new WaitCallback(this.InvalidateFrame));
            }
            catch (Exception ex)
            {
                Program.WriteLog(ex);
                MessageBox.Show(ex.ToString());
            }

            this.GraphicsDevice.DeviceLost += new EventHandler<EventArgs>(GraphicsDeviceDeviceLost);
        }
        #endregion

        protected override void OnInvalidated(InvalidateEventArgs e)
        {
            base.OnInvalidated(e);
        }

        private void InvalidateFrame(object state)
        {
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            Stopwatch sw = Stopwatch.StartNew();
            do
            {
                try
                {
                    sw.Restart();
                    this.Invalidate();
                    var toSleep = (int)((1000 / TileDisplay.FrameFrequency) - sw.ElapsedMilliseconds);
                    sw.Stop();
                    if (toSleep > 0)
                    {
                        Thread.Sleep(toSleep);
                        sw.Restart();
                    }
                }
                catch (Exception ex)
                {
                    Program.WriteLog(ex);
                }
            } while (true);
        }

        protected override void Draw()
        {
            try
            {
                lock (this.drawLocker)
                {
                    this.DrawLogic();

                    // Clear the screen.
                    API.GraphicsDevice.Clear(Color.Black);

                    // Draw current map layer
                    API.MapManager.CurrentLayer.Draw();

                    if (!API.Content.Loaded) { return; }

                    #region Draw stuff from plugins here
                    var guiOverlays = new List<GuiOverlay>();
                    foreach (var overlay in API.PluginManager.GetMapOverlays())
                    {
                        if (overlay is GuiOverlay)
                        {
                            guiOverlays.Add((GuiOverlay)overlay);
                            continue;
                        }
                        // Draw valid overlays.
                        API.FrameDrawer.Draw(overlay.MapItems);
                    }

                    foreach (var overlay in guiOverlays)
                    {
                        overlay.GenerateDimmerTexture();
                        API.FrameDrawer.DrawTexture(new MapTexture[] { overlay.DimmerTexture }, DrawMode.ViewPort);
                        API.FrameDrawer.Draw(overlay.MapItems);
                    }
                    #endregion
                }

                this.timeSinceLastDraw.Restart();


                // Call the OnDraw event, if necessary.
                var drawEvent = this.OnDraw;
                if (drawEvent != null)
                {
                    lock (drawEvent)
                    {
                        drawEvent(this, new EventArgs());
                    }
                }
            }
            catch (Exception ex)
            {
                Program.WriteLog(ex);
            }
        }

        #region Win32 API
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.HWnd != this.Handle)
            {
                return;
            }
            if (m.Msg == Win32MessageCodes.WM_MOUSEHWHEEL)
            {
                this.OnHorizontalMouseWheel(this, m.WParam.ToInt32());
                m.Result = (IntPtr)1;
            }

        }
        #endregion

        #region Mouse scrollwheel
        private void OnHorizontalMouseWheel(object sender, int value)
        {
            if (value == 0) { return; }
            if (API.Camera == null) { return; }

            if (API.State.CameraControl != CameraControl.Manual)
            {
                API.State.CameraControl = CameraControl.Manual;
            }

            float newPos = API.Camera.Center.X;
            if (value > 0)
            {
                newPos += API.UiElements.HScrollBar.SmallChange * this.mouseScrollSensitivity;
            }
            else if (value < 0)
            {
                newPos -= API.UiElements.HScrollBar.SmallChange * this.mouseScrollSensitivity;
            }
            API.Camera.CenterOnPixel(newPos, API.Camera.Center.Y);
            this.ReportMousePosition();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            var button = e.Button;
            

            if (ModifierKeys == System.Windows.Forms.Keys.Shift)
            {
                API.State.CameraControl = CameraControl.Manual;
                var newPos = (API.Camera.Center.X - (e.Delta * this.mouseScrollSensitivity / 120 * API.UiElements.HScrollBar.SmallChange));
                API.Camera.CenterOnPixel(newPos, API.Camera.Center.Y);
            }
            else if (ModifierKeys.HasFlag(System.Windows.Forms.Keys.Control))
            {
                var element = API.UiElements.ParentForm.MagnificationSlider;
                float newVal = element.Value;
                if (e.Delta > 0) { newVal++; }
                else if (e.Delta < 0) { newVal--; }
                else { return; }

                // Magnify to mouse position.
                if (ModifierKeys.HasFlag(System.Windows.Forms.Keys.Alt) && API.State.CameraControl == CameraControl.Manual)
                {
                    API.Camera.CenterOnPixel(
                        (int)API.Camera.Position.X + e.X,
                        (int)API.Camera.Position.Y + e.Y);
                }
                var curPos = API.Camera.RelativePosition();
                
                element.Value = (int)MathHelper.Clamp(
                        newVal,
                        element.Minimum,
                        element.Maximum);

                if (curPos != Vector2.Zero)
                {
                    API.Camera.CenterOnRelativePosition(curPos);
                }
            }
            else
            {
                API.State.CameraControl = CameraControl.Manual;
                var newPos = (API.Camera.Center.Y - (e.Delta * this.mouseScrollSensitivity / 120 * API.UiElements.VScrollBar.SmallChange));
                API.Camera.CenterOnPixel(API.Camera.Center.X, newPos);
            }

            this.ReportMousePosition();
            base.OnMouseWheel(e);
        }
        #endregion

        #region Mouse clicking
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            switch (e.Button)
            {
                case System.Windows.Forms.MouseButtons.Left:
                    API.Camera.CenterOnPixel(
                        (int)API.Camera.Position.X + e.X,
                        (int)API.Camera.Position.Y + e.Y);
                    this.ZoomIn();                    
                    break;
                case System.Windows.Forms.MouseButtons.Right:
                    API.Camera.CenterOnPixel(
                        (int)API.Camera.Position.X + e.X,
                        (int)API.Camera.Position.Y + e.Y);
                    this.ZoomOut();                    
                    break;
            }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            switch (e.Button)
            {
                case System.Windows.Forms.MouseButtons.Middle:
                    API.Camera.CenterOnPixel(
                        (int)API.Camera.Position.X + e.X,
                        (int)API.Camera.Position.Y + e.Y);
                    break;
            }
            base.OnMouseClick(e);
        }

       
        #endregion

        #region Mouse movement


        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (API.Camera == null) { return; }
            if (this.mousePosition == Vector2.Zero)
            {
                this.mousePosition.X = e.X;
                this.mousePosition.Y = e.Y;
            }
            var matrix = API.Camera.TransformMatrix;
            this.ReportMousePosition();

            var mouseState = Mouse.GetState();
            if (!this.isPanningMap) { return; }
            var deltaX = e.X - this.mousePosition.X;
            var deltaY = e.Y - this.mousePosition.Y;
            
            if (API.State.CameraControl != CameraControl.Manual)
            {
                // If we're not already in manual control mode, require some delta. 
                // Prevents accidential scrolling.
                if (Math.Abs(deltaX) > 15 || Math.Abs(deltaY) > 15)
                {
                    API.State.CameraControl = CameraControl.Manual;
                }
                // Otherwise, discard the scroll.
                else
                {
                    return;
                }
            }

            this.mousePosition.X = e.X;
            this.mousePosition.Y = e.Y;

            var camPos = API.Camera.Center;
            API.Camera.CenterOnPixel(
                (int)(camPos.X - deltaX),
                (int)(camPos.Y - deltaY));

            base.OnMouseMove(e);
        }

        private void ReportMousePosition()
        {
            var matrix = API.Camera.TransformMatrix;
            var mouseState = Mouse.GetState();
            API.UiElements.ParentForm.ToolStripStatusLabel1.Text = String.Format(
                "Mouse: {0}x{1}",
                mouseState.X - matrix.Translation.X,
                mouseState.Y - matrix.Translation.Y);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var mouseState = Mouse.GetState();
            if (mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                this.mousePosition.X = mouseState.X;
                this.mousePosition.Y = mouseState.Y;
                this.isPanningMap = true;
                this.Focus();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
             var mouseState = Mouse.GetState();
            if (mouseState.LeftButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed)
            {
                this.mousePosition = Vector2.Zero;
                this.isPanningMap = false;
            }
            base.OnMouseUp(e);
        }
        #endregion


        protected override void OnResize(EventArgs e)
        {
            if (API.Camera == null) { return; }
            //this.Invalidate();            
            API.Camera.AdjustScrollbarsToLayer();
            this.ReportMousePosition();
            base.OnResize(e);
        }

        #region Keyboard input
        protected override void OnKeyDown(KeyEventArgs e)
        {
            this.HandleKeyDown(e);
            if (!e.SuppressKeyPress)
            {
                base.OnKeyDown(e);
            }
        }


        public void HandleKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case System.Windows.Forms.Keys.W:
                case System.Windows.Forms.Keys.A:
                case System.Windows.Forms.Keys.S:
                case System.Windows.Forms.Keys.D:
                    this.PanMap(e);
                    break;
                case System.Windows.Forms.Keys.Oemplus:
                case System.Windows.Forms.Keys.Add:
                    e.SuppressKeyPress = true;
                    this.ZoomIn();
                    break;
                case System.Windows.Forms.Keys.OemMinus:
                case System.Windows.Forms.Keys.Subtract:
                    e.SuppressKeyPress = true;
                    this.ZoomOut();
                    return;
                case System.Windows.Forms.Keys.Zoom:
                    e.SuppressKeyPress = true;
                    if (!e.Control)
                    {
                        this.ZoomIn();
                    }
                    else
                    {
                        this.ZoomOut();
                    }
                    break;
            }
        }

        private void PanMap(KeyEventArgs e)
        {
            if (e.Control || e.Alt) { return; }
            if (API.Camera == null) { return; }
            var x = 0;
            var y = 0;
            if (e.KeyData == System.Windows.Forms.Keys.W)
            {
                y -= 1;
                e.SuppressKeyPress = true;
            }
            if (e.KeyData == System.Windows.Forms.Keys.S)
            {
                y += 1;
                e.SuppressKeyPress = true;
            }
            if (e.KeyData == System.Windows.Forms.Keys.A)
            {
                x -= 1;
                e.SuppressKeyPress = true;
            }
            if (e.KeyData == System.Windows.Forms.Keys.D)
            {
                x += 1;
                e.SuppressKeyPress = true;
            }

            if (e.SuppressKeyPress)
            {
                API.Camera.CenterOnPixel(
                    API.Camera.Center.X + API.UiElements.HScrollBar.SmallChange * x,
                    API.Camera.Center.Y + API.UiElements.VScrollBar.SmallChange * y);
                this.ReportMousePosition();
                return;
            }

            base.OnKeyDown(e);
        }
        #endregion

        public void ZoomIn()
        {
            if (API.MapManager == null) { return; }
            API.MapManager.ZoomIn();
            this.ReportMousePosition();
        }

        public void ZoomOut()
        {
            if (API.MapManager == null) { return; }
            API.MapManager.ZoomOut();
            this.ReportMousePosition();
        }


        private void DrawLogic()
        {
            if (API.Camera == null) { return; }
            switch (API.State.CameraControl)
            {
                case CameraControl.SelectedCharacters:
                    MoveCameraToSelectedCharacters();
                    break;
                case CameraControl.ActiveCharacter:
                    this.MoveCameraToActiveCharacter();
                    break;
                case CameraControl.Manual:
                    API.Camera.CenterOnScrollbars();
                    break;
            }
        }

        /// <summary>
        /// Centers camera on whichever character locator is currently being controlled by the player.
        /// </summary>
        private void MoveCameraToActiveCharacter()
        {
            API.AoHook.UpdateTrackedDimension();
            try
            {
                var id = API.AoHook.GetActiveCharacter();
                if (id == null)
                {
                    if (this.lastActiveID != null)
                    {
                        id = lastActiveID;
                    }
                    else
                    {
                        return;
                    }
                }

                if (!API.State.PlayerInfo.ContainsKey(id)) { return; }
                var playerInfo = new PlayerInfo[] { API.State.PlayerInfo[id] };
                this.MoveCameraToCharacterLocators(playerInfo);
            }
            catch (Exception ex)
            {
                Program.WriteLog(ex);
            }
        }

        /// <summary>
        /// Centers camera on user-selected character locator(s)
        /// </summary>
        private void MoveCameraToSelectedCharacters()
        {
            try
            {
                var playerInfo = API.State.PlayerInfo.Values.ToArray().Where(i => i.IsTrackedByCamera).ToArray();
                if (playerInfo.Length == 0) { return; }
                this.MoveCameraToCharacterLocators(playerInfo);
            }
            catch (Exception ex)
            {
                Program.WriteLog(ex);
            }
        }

        private void MoveCameraToCharacterLocators(PlayerInfo[] characters)
        {
            try 
            {
                var vectors = new List<Vector2>();
                int shadowlandsCharacters = 0;
                int rubikaCharacters = 0;
                foreach (var item in characters)
                {
                    var info = item as PlayerInfo;

                    // Track rk/sl characters
                    if (info.InShadowlands)
                    {
                        shadowlandsCharacters++;
                    }
                    else
                    {
                        rubikaCharacters++;
                    }

                    if (info.InShadowlands && API.MapManager.CurrentMap.Type != MapType.Shadowlands) { continue; }

                    var charPos = API.MapManager.GetPosition(info.Zone.ID, info.Position.X, info.Position.Z);
                    if (charPos == Vector2.Zero) { continue; }
                    vectors.Add(charPos);
                }

                if (API.State.MapTypeAutoSwitching)
                {
                    if (rubikaCharacters > shadowlandsCharacters && API.MapManager.CurrentMap.Type != MapType.Rubika)
                    {
                        API.MapManager.FindAvailableMaps(MapType.Rubika);
                        API.MapManager.SelectMap(MapType.Rubika);
                    }
                    else if (shadowlandsCharacters > rubikaCharacters && API.MapManager.CurrentMap.Type != MapType.Shadowlands)
                    {
                        API.MapManager.FindAvailableMaps(MapType.Shadowlands);
                        API.MapManager.SelectMap(MapType.Shadowlands);
                    }
                }

                if (vectors.Count == 0) { return; }

                API.Camera.CenterOnVectors(vectors.ToArray());
            }
            catch (Exception ex)
            {
                Program.WriteLog(ex);
            }
        }
    }
}
