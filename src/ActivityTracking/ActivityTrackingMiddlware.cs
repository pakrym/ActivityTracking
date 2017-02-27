using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ActivityTracking
{
    public class ActivityTrackingMiddlware
    {
        private RequestDelegate _next;
        private HttpMethodOverrideOptions _options;
        private DiagnosticSource httpListener = new DiagnosticListener("");

        public ActivityTrackingMiddlware(RequestDelegate next)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (httpListener.IsEnabled("Http_In"))
            {
                Activity activity = new Activity("Http_In");
                activity.SetStartTime(DateTime.UtcNow);
                //add tags, baggage, etc.
                activity.SetParentId(context.Request.Headers["x-ms-request-id"]);
                foreach (var header in context.Request.Headers)
                {

                    if (header.Key.StartsWith("x-baggage-"))
                    activity.AddBaggage(header.Key, header.Value);
                }

                httpListener.StartActivity(activity, context);
                try
                {
                    await _next(context);
                }
                finally
                {
                    var stopTime = DateTime.UtcNow;
                    activity.SetEndTime(stopTime);
                    //stop activity
                    httpListener.StopActivity(activity, stopTime);
                }
            }
        }
    }
}
