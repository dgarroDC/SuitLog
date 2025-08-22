using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using OWML.Utils;

namespace SuitLog;

// Copied from CSLM
public static class TranslationHandler
{
    private static Dictionary<TextTranslation.Language, Dictionary<string, string>> _translations = null;

    private static void Setup()
    {
        _translations = new Dictionary<TextTranslation.Language, Dictionary<string, string>>();
        foreach (var language in EnumUtils.GetValues<TextTranslation.Language>())
        {
            var file = Path.Combine(SuitLog.Instance.ModHelper.Manifest.ModFolderPath, "translations", language.ToString().ToLowerInvariant() + ".json");
            try
            { 
                _translations[language] = JObject.Parse(File.ReadAllText(file)).ToObject<Dictionary<string, string>>();
            }
            catch(Exception e)
            {
                // Ignore
            }
        }
    }
    
    public static string GetTranslation(string key)
    {
        if (_translations == null)
        {
            Setup();
        }

        TextTranslation.Language currentLanguage = TextTranslation.Get().GetLanguage();
        TextTranslation.Language defaultLanguage = TextTranslation.Language.ENGLISH;
        if (_translations.ContainsKey(currentLanguage) && _translations[currentLanguage].ContainsKey(key))
        {
            return  _translations[currentLanguage][key];
        }
        if (_translations.ContainsKey(defaultLanguage) && _translations[defaultLanguage].ContainsKey(key))
        {
            return _translations[defaultLanguage][key];
        }
        else
        {
            return key;
        }
    }
}