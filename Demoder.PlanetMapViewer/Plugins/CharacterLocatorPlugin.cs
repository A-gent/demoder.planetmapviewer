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
using System.Linq;
using System.Text;
using Demoder.PlanetMapViewer.PmvApi;
using Demoder.PlanetMapViewer.DataClasses;
using Microsoft.Xna.Framework;
using Demoder.AoHook;
using System.Threading;
using System.ComponentModel;
using Demoder.Common.Attributes;

namespace Demoder.PlanetMapViewer.Plugins
{
    [Plugin("Character Locator")]
    [Description("Displays character locators on the planet map")]
    public class CharacterLocatorPlugin : IPlugin
    {
        #region settings
        [Setting(LoadedFont.Silkscreen7)]
        public LoadedFont CharacterLocatorFont { get; set; }

        [Setting(true)]
        public bool DrawOfflineCharacters { get; set; }

        [Setting(false)]
        public bool DrawCharacterDimension { get; set; }

        [Setting(true)]
        public bool HighlightActiveCharacter { get; set; }
        #endregion

        public CharacterLocatorPlugin()
        {
        }

        
        #region Retrieve IMapItems
        
        public IEnumerable<MapOverlay> GetCustomOverlay()
        {
            var overlay = new MapOverlay
            {
                DrawOrder = int.MaxValue
            };

            overlay.MapItems.AddRange(this.GetCharacterLocators());
            return new MapOverlay[] { overlay };
        }

        private IMapItem[] GetCharacterLocators()
        {
            var mapTextureTexts = new List<IMapItem>();
            if (API.State.PlayerInfo.Count == 0)
            {
                return new MapTexture[0];
            }

            var characters = API.State.PlayerInfo.Values.ToArray();

            IMapItem activeTex = null;

            foreach (var character in characters)
            {
                var activeCharacter = API.AoHook.GetActiveCharacter();
                bool isActive = character.Identity == activeCharacter;
                if (character == null || character.Zone == null || character.Position == null || character.Name == null) { continue; }
                if (!character.IsHooked && !this.DrawOfflineCharacters)
                {
                    // We ain't tracking offline characters.
                    continue;
                }

                var charColor = Color.Yellow;
                if (this.HighlightActiveCharacter && !isActive)
                {
                    charColor = new Color(255, 255, 156);   
                }

                var texture = new MapTexture()
                {
                    Texture = API.Content.Textures.CharacterLocator,
                    KeyColor = charColor,
                    Position = new PositionDefinition(character.Zone.ID, character.Position.X, character.Position.Z),
                    PositionAlignment = MapItemAlignment.Center,
                };

                if (!character.IsHooked)
                {
                    texture.KeyColor = Color.Gray;
                }

                var charRealPos = texture.Position.GetPosition();

                var txt = new MapText
                {
                    Position = new PositionDefinition(),
                    PositionAlignment = MapItemAlignment.Center,
                    Text = character.Name,
                    TextColor = charColor,
                    OutlineColor = Color.Black,
                    Outline = true,
                    Font = API.Content.Fonts.GetFont(this.CharacterLocatorFont)
                };

                if (this.DrawCharacterDimension)
                {
                    txt.Text += " (" + character.Dimension.ToString().Substring(0, 1) + ")";
                }

                if (!character.IsHooked)
                {
                    txt.TextColor = Color.LightGray;
                    txt.OutlineColor = Color.Gray;
                }
                var mpt = new MapTextureText(texture, txt, 1)
                {
                    PositionAlignment = MapItemAlignment.Bottom | MapItemAlignment.Center,
                };

                if (!isActive)
                {
                    mapTextureTexts.Add(mpt);
                }
                else
                {
                    activeTex = mpt;
                }
            }

            if (activeTex != null)
            {
                mapTextureTexts.Add(activeTex);
            }
            return mapTextureTexts.ToArray();
        }

        #endregion

        public void Dispose()
        {

        }
    }
}
