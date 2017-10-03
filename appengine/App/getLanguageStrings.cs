#region

using common.config;
using System.Collections.Generic;
using System.IO;

#endregion

namespace appengine.app
{
    internal class getLanguageStrings : RequestHandler
    {
        internal static string appengine = Settings.NETWORKING.APPENGINE_URL;

        protected Dictionary<string, string> _ = new Dictionary<string, string>
        {
            { "en", "english" }
        };

        protected override void HandleRequest()
        {
            string _lt = Query["languageType"];

            if (_lt == null)
                return;
            
            string language = null;
            if (_.TryGetValue(_lt, out language))
                WriteLine(File.ReadAllText($"app/languages/{language}.json"), false);
            else
                WriteLine(File.ReadAllText($"app/languages/english.json"), false);
        }
    }
}