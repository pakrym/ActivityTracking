using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Linq;
using System.Net.Http.Headers;

namespace ActivityTracking
{
    public class ActivityTrackingMiddlware
    {
        private readonly RequestDelegate _next;
        private readonly DiagnosticListener _httpListener = new DiagnosticListener("Microsoft.AspNetCore");
		private const string ActivityName = "Microsoft.AspNetCore.Activity";
		private const string ActivityStartName = "Microsoft.AspNetCore.Activity.Start";

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
			Activity activity = null;
			if ( _httpListener.IsEnabled() &&  //quick check that someone is listening
				 _httpListener.IsEnabled(ActivityName)) //someone listening to activity events
			{
                activity = new Activity(ActivityName);

				//add tags, baggage, etc.
				StringValues requestId;
				if (context.Request.Headers.TryGetValue("Request-Id", out requestId))
				{
					activity.SetParentId(requestId.First());
					StringValues baggage;
					if (context.Request.Headers.TryGetValue("Correlation-Context", out baggage))
					{
						foreach (var item in baggage)
						{
							NameValueHeaderValue baggageItem;
							if (NameValueHeaderValue.TryParse(item, out baggageItem))
							{
								activity.AddBaggage(baggageItem.Name, baggageItem.Value);
							}
						}
					}
				}

			
				//before starting an activity, check that user wants this request to be instumented
				if (_httpListener.IsEnabled(ActivityName, activity, context))
				{
					if (_httpListener.IsEnabled(ActivityStartName)) //allow Stop events only to reduce verbosity, but start activity anyway
					{
						//TODO: will we ever need to pass something else than the context? do we really need anonymous object?
						_httpListener.StartActivity(activity, new { Context = context });
					}
					else
					{
						activity.Start();
					}
				}
            }

			Task next = _next(context);
			try
			{
				await next.ConfigureAwait(false);
			}
			finally
			{
				if (activity != null)
				{
					activity.SetEndTime(DateTime.UtcNow);
					_httpListener.StopActivity(activity, new { Context = context, Status = next.Status });
				}
			}
		}
	}
}
