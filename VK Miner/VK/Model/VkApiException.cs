using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VK_Miner.VK.Model
{
    class VkApiException : Exception
    {
        public enum ErrorType
        {
            UnknownError = 1,
            ApplicationIsDisabled = 2,
            UnknownMethodPassed = 3,
            IncorrectSignature = 4,
            UserAuthorizationFailed = 5,
            TooManyRequestsPerSecond = 6,
            PermissionToPerformThisActionIsDenied = 7,
            InvalidRequest = 8,
            FloodControl = 9,
            InternalServerError = 10,
            InTestModeApplicationShouldBeDisabledOrUserShouldBeAuthorized = 11,
            CaptchaNeeded = 14,
            AccessDenied = 15,
            HttpAuthorizationFailed = 16,
            ValidationRequired = 17,
            ConfirmationRequired = 24,
            OneOfTheParametersSpecifiedWasMissingOrInvalid = 100,
            InvalidUserId = 113,
            InvalidTimestamp = 150,
            PermissionDenied = 600
        }

        public ErrorType ErrorCode { get; private set; }
        public string ErrorMsg { get; private set; }

        public KeyValuePair<string, string>[] RequestParams { get; private set; }

        public VkApiException(JToken json)
        {
            ErrorCode = (ErrorType)json["error_code"].Value<int>();
            ErrorMsg = json["error_msg"].Value<string>();
            RequestParams = json["request_params"].Value<JArray>()
                .AsEnumerable()
                .Select(i => new KeyValuePair<string, string>(i["key"].Value<string>(), i["value"].Value<string>()))
                .ToArray();
        }
    }
    class CaptchaException : VkApiException
    {
        public string CaptchaSid { get; private set; }
        public string CaptchaImg { get; private set; }

        public CaptchaException(JToken json) : base(json)
        {
            CaptchaSid = json["captcha_sid"].Value<string>();
            CaptchaImg = json["captcha_img"].Value<string>();
        }
    }
}
