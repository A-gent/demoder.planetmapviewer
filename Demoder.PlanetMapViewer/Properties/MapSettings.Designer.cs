﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.261
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Demoder.PlanetMapViewer.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "10.0.0.0")]
    internal sealed partial class MapSettings : global::System.Configuration.ApplicationSettingsBase {
        
        private static MapSettings defaultInstance = ((MapSettings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new MapSettings())));
        
        public static MapSettings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string AoPath {
            get {
                return ((string)(this["AoPath"]));
            }
            set {
                this["AoPath"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Shadowlands\\ShadowlandsMap.txt")]
        public string SelectedShadowlandsMap {
            get {
                return ((string)(this["SelectedShadowlandsMap"]));
            }
            set {
                this["SelectedShadowlandsMap"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("normal\\PlanetMapIndexNormal.txt")]
        public string SelectedRubikaMap {
            get {
                return ((string)(this["SelectedRubikaMap"]));
            }
            set {
                this["SelectedRubikaMap"] = value;
            }
        }
    }
}
