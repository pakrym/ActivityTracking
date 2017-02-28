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
        private readonly RequestDelegate _next;
        private readonly DiagnosticSource _httpListener = new DiagnosticListener("AspNetCoreActivityTracking");

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
            if (_httpListener.IsEnabled("Http_In"))
            {
                Activity activity = new Activity("Http_In");
                activity.SetStartTime(DateTime.UtcNow);
                //add tags, baggage, etc.
                activity.SetParentId(context.Request.Headers["request-id"]);
                foreach (var header in context.Request.Headers)
                {
                    if (header.Key.StartsWith("baggage-"))
                    {
                        activity.AddBaggage(header.Key, header.Value);
                    }
                }

                var shouldStart = _httpListener.IsEnabled("Http_In", activity, context);
                if (shouldStart)
                {
                    _httpListener.StartActivity(activity, context);
                }

                try
                {
                    await _next(context);
                }
                finally
                {
                    if (shouldStart)
                    {
                        var stopTime = DateTime.UtcNow;
                        activity.SetEndTime(stopTime);
                        //stop activity
                        _httpListener.StopActivity(activity, stopTime);
                    }
                }
            }
            else
            {
                await _next(context);
            }
        }
    }
}
