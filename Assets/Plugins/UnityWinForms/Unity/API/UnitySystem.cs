﻿using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityWinForms.Core.API;
using UnityWinForms.System.Drawing;

namespace UnityWinForms.Unity.API
{
    public class UnitySystem : IApiSystem
    {
        public CultureInfo CurrentCulture
        {
            get
            {
                var language = Application.systemLanguage;
                string name;
                switch (language)
                {
                    case SystemLanguage.Afrikaans: name = "af-ZA"; break;
                    case SystemLanguage.Arabic: name = "ar-AE"; break;
                    case SystemLanguage.Basque: name = "eu-ES"; break;
                    case SystemLanguage.Belarusian: name = "be-BY"; break;
                    case SystemLanguage.Bulgarian: name = "bg-BG"; break;
                    case SystemLanguage.Catalan: name = "ca-ES"; break;
                    case SystemLanguage.Chinese: name = "zh-CN"; break;
                    case SystemLanguage.ChineseSimplified: name = "zh-CHS"; break;
                    case SystemLanguage.ChineseTraditional: name = "zh-CHT"; break;
                    case SystemLanguage.Czech: name = "cs-CZ"; break;
                    case SystemLanguage.Danish: name = "da-DK"; break;
                    case SystemLanguage.Dutch: name = "nl-BE"; break;
                    case SystemLanguage.Estonian: name = "et-EE"; break;
                    case SystemLanguage.Faroese: name = "fo-FO"; break;
                    case SystemLanguage.Finnish: name = "fi-FI "; break;
                    case SystemLanguage.French: name = "fr-FR"; break;
                    case SystemLanguage.German: name = "de-DE"; break;
                    case SystemLanguage.Greek: name = "el-GR"; break;
                    case SystemLanguage.Hebrew: name = "he-IL"; break;
                    case SystemLanguage.Hungarian: name = "hu-HU"; break;
                    case SystemLanguage.Icelandic: name = "is-IS"; break;
                    case SystemLanguage.Indonesian: name = "id-ID"; break;
                    case SystemLanguage.Italian: name = "it-IT"; break;
                    case SystemLanguage.Japanese: name = "ja-JP"; break;
                    case SystemLanguage.Korean: name = "ko-KR"; break;
                    case SystemLanguage.Latvian: name = "lv-LV"; break;
                    case SystemLanguage.Lithuanian: name = "lt-LT"; break;
                    case SystemLanguage.Norwegian: name = "nb-NO"; break;
                    case SystemLanguage.Polish: name = "pl-PL"; break;
                    case SystemLanguage.Portuguese: name = "pt-PT"; break;
                    case SystemLanguage.Romanian: name = "ro-RO"; break;
                    case SystemLanguage.Russian: name = "ru-RU"; break;
                    case SystemLanguage.SerboCroatian: name = "Lt-sr-SP"; break;
                    case SystemLanguage.Slovak: name = "sk-SK"; break;
                    case SystemLanguage.Slovenian: name = "sl-SI"; break;
                    case SystemLanguage.Spanish: name = "es-ES"; break;
                    case SystemLanguage.Swedish: name = "sv-SE"; break;
                    case SystemLanguage.Thai: name = "th-TH"; break;
                    case SystemLanguage.Turkish: name = "tr-TR"; break;
                    case SystemLanguage.Ukrainian: name = "uk-UA"; break;
                    case SystemLanguage.Vietnamese: name = "vi-VN"; break;

                    default:
                        name = "en-GB";
                        break;
                }

                return CultureInfo.GetCultures(CultureTypes.AllCultures).FirstOrDefault(c => c.Name == name);
            }
        }
        public Point MousePosition 
        { 
            get
            {
                float mx, my;
                if (Event.current != null)
                {
                    mx = Event.current.mousePosition.x;
                    my = Event.current.mousePosition.y;
                }
                else
                {
                    var mp = UnityEngine.Input.mousePosition;
                    mx = mp.x;
                    my = UnityEngine.Screen.height - mp.y;
                }

                var sx = global::UnityWinForms.System.Windows.Forms.Application.ScaleX;
                var sy = global::UnityWinForms.System.Windows.Forms.Application.ScaleY;
                
                return new Point((int) (mx / sx), (int) (my / sy));
            }
        }
    }
}
