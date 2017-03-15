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
				 _httpListener.IsEnabled(ActivityName)) //someone listening to activity events, in Hosting, check for "Microsoft.AspNetCore.Hosting.EndRequest" instad of ActivityName
            {
                activity = new Activity(ActivityName); // in Hosting use the same "Microsoft.AspNetCore.Activity" or something like that

                //add tags, baggage, etc.
                StringValues requestId;
                if (context.Request.Headers.TryGetValue("Request-Id", out requestId))
                {
                    try
                    {
                        //may throw ArgumentException
                        activity.SetParentId(requestId.First());
                    }
                    catch (ArgumentException)
                    {
                        //this indicates invalid parent id
                        //if there is a way to log it, do it and ignore exception: there will be no parent id for this activity
                    }

                    //we expect baggage to be empty by default - only very advanced users will be using it in near future, we encouradge them to keep baggage small (few items)
                    string[] baggage = context.Request.Headers.GetCommaSeparatedValues("Correlation-Context");
                    if (baggage != StringValues.Empty)
                    {
                        foreach (var item in baggage)
                        {
                            NameValueHeaderValue baggageItem;
                            if (NameValueHeaderValue.TryParse(item, out baggageItem))
                            {
                                //may throw ArgumentException
                                try
                                {
                                    activity.AddBaggage(baggageItem.Name, baggageItem.Value);
                                }
                                catch (ArgumentException)
                                {
                                    //this indicates invalid baggage item
                                    //if there is a way to log it, do it and ignore, invalid baggage is ignored
                                }
                            }
                        }
                    }
                }

                //before starting an activity, check that user wants this request to be instrumented
                if (_httpListener.IsEnabled(ActivityName, activity, context))
                {
                    //in Hosting, send Microsoft.AspNetCore.Hosting.BeginRequest
                    //
                    // activity.Start();
                    // if (_httpListener.IsEnabled("Microsoft.AspNetCore.Hosting.BeginRequest")) //allow Stop events only to reduce verbosity, but start activity anyway
                    //    _httpListener.Write("Microsoft.AspNetCore.Hosting.BeginRequest", new { httpContext = context, timestamp = Stopwatch.GetTimestamp() });
                    if (_httpListener.IsEnabled(ActivityStartName))
                    {
                        _httpListener.StartActivity(activity, new { httpContext = context, timestamp = Stopwatch.GetTimestamp() });
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
                    //in Hosting, send Microsoft.AspNetCore.Hosting.EndRequest.
                    //in HostingApplication.DisposeContext you don't have Activity object created before
                    //you need to traverse to the top most activity with something like
                    //
                    //var rootActivity = Activity.Current;
                    //if (rootActivity != null)
                    //{
                    //    while (rootActivity.Parent != null)
                    //    {
                    //       rootActivity = rootActivity.Parent;
                    //    }
                    //    rootActivity.SetEndTime(DateTime.UtcNow);
                    //    rootActivity.Stop();
                    //    _httpListener.StopActivity("Microsoft.AspNetCore.Hosting.EndRequest", new { httpContext = context, timestamp = Stopwatch.GetTimestamp(), Status = next.Status });
                    //}
                    // Note that EndRequest must be sent even if request was cancelled/threw an exception

                    activity.SetEndTime(DateTime.UtcNow);
                    _httpListener.StopActivity(activity, new { httpContext = context, Status = next.Status });
				}
			}
		}
	}
}
