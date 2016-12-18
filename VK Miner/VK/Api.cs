using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace VK_Miner.VK
{
    public sealed class Api
    {
        private const string Root = "https://api.vk.com/method/";

        private const int CallDelay = 400;

        public string AccessToken { get; }
        public long UserId { get; }
        public string Version { get; }

        private readonly Stopwatch _callStopwatch;
        private readonly object[] _accessTokenArgs;

        public int DelayUntilNextCall => Math.Max(0, (int)(CallDelay - _callStopwatch.ElapsedMilliseconds));

        public Api(string accessToken, long userId, string version = "5.52")
        {
            AccessToken = accessToken;
            UserId = userId;
            Version = version;

            _accessTokenArgs = new object[]
            {
                "v", Version,
                "access_token", AccessToken
            };

            _callStopwatch = Stopwatch.StartNew();
        }

        public JToken Run(string method, params object[] args)
        {
            lock (_accessTokenArgs)
            {
                var delay = CallDelay - _callStopwatch.ElapsedMilliseconds;
                if (delay > 0) Thread.Sleep((int)delay);
                var response = ExecuteCollection(method, args.Concat(_accessTokenArgs));
                _callStopwatch.Restart();
                return response;
            }
        }

        public JToken Get(string method, params object[] args)
        {
            lock (_accessTokenArgs)
            {
                IEnumerable<object> localArgs = args.Concat(_accessTokenArgs);

                while (true)
                {
                    var delay = CallDelay - _callStopwatch.ElapsedMilliseconds;
                    if (delay > 0) Thread.Sleep((int)delay);

                    var response = ExecuteCollection(method, localArgs);

                    _callStopwatch.Restart();

                    var error = response["error"];

                    if (error == null || error.Value<int>("error_code") != 14)
                        return response["response"];

                    var window = new CaptchaWindow(error.Value<string>("captcha_img"));
                    if (window.ShowDialog() == true)
                    {
                        localArgs = args.Concat(_accessTokenArgs).Concat(new object[]
                        {
                            "captcha_sid", error.Value<string>("captcha_sid"),
                            "captcha_key", window.CaptchaKey
                        });
                    }
                }
            }
        }

        public static JToken Execute(string method, params object[] args)
        {
            return ExecuteCollection(method, args);
        }

        public static JToken ExecuteCollection(string method, IEnumerable<object> args)
        {
            var e = args.GetEnumerator();

            var values = new NameValueCollection();
            while (e.MoveNext())
            {
                var key = e.Current;

                if (!e.MoveNext())
                    throw new ArgumentException(nameof(args));

                var value = e.Current;
                values[key.ToString()] = value.ToString();
            }

            string response;
            using (var client = new WebClient { Encoding = Encoding.UTF8 })
                response = Encoding.UTF8.GetString(client.UploadValues(Root + method, values));

            return JToken.Parse(response);
        }

        public static Api Auth(int clientId, string scope = "", string version = "5.52", bool revoke = false, string display = "page")
        {
            var window = new LoginWindow(clientId, version, scope, display, revoke);

            return window.ShowDialog() == true ? new Api(window.AccessToken, window.UserId, version) : null;
        }

        public static Api CacheOrCreate(string appName, int clientId, string scope = "", string version = "5.52", bool revoke = false, string display = "page")
        {
            const string regValue = "lastEntry";
            var regPath = $@"HKEY_CURRENT_USER\SOFTWARE\{appName}";

            if (!revoke)
            {
                var value = Registry.GetValue($@"HKEY_CURRENT_USER\SOFTWARE\{appName}", regValue, null) as string;
                if (value != null)
                {
                    var parts = value.Split('&');
                    int userId;
                    if (parts.Length == 5 &&
                        parts[0] == clientId.ToString() &&
                        parts[1] == scope &&
                        int.TryParse(parts[2], out userId) &&
                        parts[4] == version)
                    {
                        var response = Execute("users.get", "access_token", parts[3], "v", parts[4]);
                        if (response["response"]?.FirstOrDefault()?.Value<int>("id") == userId)
                        {
                            return new Api(parts[3], userId, parts[4]);
                        }
                    }
                }
            }

            var api = Auth(clientId, scope, version, revoke, display);

            if (api != null)
                Registry.SetValue(regPath, regValue, $"{clientId}&{scope}&{api.UserId}&{api.AccessToken}&{version}");

            return api;
        }
    }
}
