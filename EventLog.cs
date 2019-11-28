using Newtonsoft.Json;
using System;

namespace HAS.Events
{
    public class EventLog
    {
        public string Id { get; set; }
        public DateTime CaptureDate { get; set; }
        public string Assembly { get; set; }
        public string Event { get; set; }
        public object Message { get; set; }
        public object Result { get; set; }
        public int Status { get; set; }
        public string Env { get; set; }

        public static EventLog Create(string eventName, object result, object msg, int status, string env)
        {
            return new EventLog
            {
                CaptureDate = DateTime.UtcNow,
                Assembly = "HAS.Events",
                Event = eventName,
                Message = msg,
                Result = result,
                Status = status,
                Env = env.ToUpper(),
            };
        }
    }

    public static class MPYLogEventExtensions
    {
        public static string ToJson(this EventLog eventObj)
        {
            return JsonConvert.SerializeObject(eventObj);
        }
    }
}
