using System.Collections.Generic;

namespace SeprrAPI.Contract
{
    public class SlackRequest
    {
        public string text { get; set; }
    }
    public class SlackResponse
    {
        public string text { get; set; }
        public List<Attachments> attachments { get; set; }
    }

    public class Attachments
    {
        public string title { get; set; }
        public string text { get; set; }
        public string color { get; set; }
        public string[] mrkdwn_in { get; set; } = { "text", "fields" };
        public List<Fields> fields { get; set; }
        public string ts { get; set; }
        public string footer { get; set; }
    }

    public class Fields
    {
        public string title { get; set; }
        public string value { get; set; }
    }

    public class ApiResponse
    {
        public string orig_train { get; set; }
        public string orig_departure_time { get; set; }
        public string orig_line { get; set; }
        public string orig_arrival_time { get; set; }
        public string orig_delay { get; set; }
        public string isdirect { get; set; }
    }
}
