using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SeprrAPI.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace SeprrAPI
{
    public static class NextThreeTrainFunction
    {
        /// <summary>
        /// Sends next three available septa regional rail.
        /// </summary>
        /// <param name="request">Slack request in the format as "rr sourceStation-destinationStation"</param>
        /// <param name="log"></param>
        /// <returns>Slack fomatted response</returns>
        /// <example>
        /// Slack Request :
        /// rr dev-30
        /// Slack Response :
        /// Next available trains
        ///    On Paoli/Thorndale Line From Devon To 30th Street Station
        ///    Train # 562
        ///    Departure time :  1:22PM  | delayed by 3 mins
        ///    Train # 564
        ///    Departure time :  2:04PM  | On time
        ///    Train # 566
        ///    Departure time :  2:23PM  | On time
        ///    Seprr © 2020 | Feb 14th
        /// </example>
        [FunctionName("NextThreeTrainFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest request,
            ILogger log)
        {
            log.LogInformation($"GetRegionalRailRequest Start");

            SlackResponse slackResponse = null;
            try
            {
                // Get request body and parse it into json
                string bodyContent = await request.ReadAsStringAsync();
                var values = HttpUtility.ParseQueryString(bodyContent);
                var jsonContent = JsonConvert.SerializeObject(values.AllKeys.ToDictionary(k => k, k => values[k]));

                var incoming = JsonConvert.DeserializeObject<SlackRequest>(jsonContent);

                //read the incomimg header
                var command = incoming.text;
                log.LogInformation($"Request Query {command}");

                // read request : First 2 characters are the trigger command
                // rr : trigger command in slack
                string commandHeader =
                !String.IsNullOrWhiteSpace(command) && command.Length >= 2
                ? command.Substring(0, 2)
                : command;

                // call septa api to fetch the latest details
                var responseMsg = String.Empty;
                var FromStation = "30th Street Station"; // default/falback station
                var ToStation = "30th Street Station";  // default/falback station
                if (commandHeader.ToLower() == "rr")
                {
                    string commandStation =
                        !String.IsNullOrWhiteSpace(command) && command.Length > 2
                        ? command.Substring(2, command.Length - 2).Trim()
                        : "-30th Street Station";

                    // request string fomat parsing 
                    // rr {sourceDestination}-{destinationStation}
                    string[] dataStation = commandStation.Split(new[] { '-' }, 2);

                    //if source/origination station exists
                    if (dataStation.ElementAtOrDefault(0) != null && !string.IsNullOrWhiteSpace(dataStation[0]))
                    {
                        FromStation = ResolveStationName(dataStation[0].Trim());
                    }

                    //To Station
                    //if from station exists
                    if (dataStation.ElementAtOrDefault(1) != null && !string.IsNullOrWhiteSpace(dataStation[1]))
                    {
                        ToStation = ResolveStationName(dataStation[1].Trim());
                    }
                    responseMsg = CallSeptaApi(log, FromStation, ToStation);
                }

                // format slack response 
                slackResponse = FormatSlackResponse(responseMsg, FromStation, ToStation);

                log.LogInformation("GetRegionalRailRequest Complete.");
            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message, null);

                // return error response
                slackResponse = FormatErrorSlackResponse(ex.Message);
                return new OkObjectResult(slackResponse);
            }

            // return success response
            return new OkObjectResult(slackResponse);
        }

        /// <summary>
        /// Format Slack error response
        /// </summary>
        /// <param name="errorDescription">Error to be sent to slack for display</param>
        /// <returns>Slack formatted  error response</returns>
        private static SlackResponse FormatErrorSlackResponse(string errorDescription)
        {
            var slackResponse = new SlackResponse();
            slackResponse.text = "Next available trains";

            // create attachment object 
            var attachmentList = new List<Attachments>();
            var attachment = new Attachments();
            attachment.color = "#1E98D1";
            attachment.footer = "Seprr © 2020";
            attachment.ts = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds.ToString();
            var fieldList = new List<Fields>();

            attachment.text = $"*There has been an error processing your request. Please try again.*";

            var field = new Fields();
            field.value = $"* Details:* {errorDescription}";
            field.title = $"Error Info # ";
            fieldList.Add(field);

            attachment.fields = fieldList;
            attachmentList.Add(attachment);
            slackResponse.attachments = attachmentList;
            return slackResponse;
        }

        /// <summary>
        /// Format slack response
        /// </summary>
        /// <param name="responseMessage">Response received from Septa API</param>
        /// <param name="sourceStation">string</param>
        /// <param name="destStation">string</param>
        /// <returns>Slack formatted response for propoer slack rendering of response by slack channel</returns>
        private static SlackResponse FormatSlackResponse(string responseMessage, string sourceStation, string destStation)
        {
            var slackResponse = new SlackResponse();
            slackResponse.text = "Next available trains";

            var attachmentResponse = JsonConvert.DeserializeObject<List<ApiResponse>>(responseMessage);

            //create attachment object 
            var attachmentList = new List<Attachments>();
            var attachment = new Attachments();
            attachment.color = "#1E98D1";
            attachment.footer = "Seprr © 2020";
            attachment.ts = (DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds.ToString();
            var fieldList = new List<Fields>();

            foreach (var item in attachmentResponse)
            {
                attachment.text = $"*On* {item.orig_line} Line *From* {sourceStation} *To* {destStation}";

                var filed = new Fields();
                if (item.orig_delay.ToLower() == "on time")
                {
                    filed.value = $"*Departure time : {item.orig_departure_time}*  | `{item.orig_delay}`";
                }
                else
                {
                    filed.value = $"*Departure time : {item.orig_departure_time}*  | `delayed by {item.orig_delay}`";
                }
                filed.title = $"Train # {item.orig_train}";
                fieldList.Add(filed);
            }
            attachment.fields = fieldList;
            attachmentList.Add(attachment);
            slackResponse.attachments = attachmentList;
            return slackResponse;
        }

        /// <summary>
        /// Makes a resy call  to Septa api
        /// </summary>
        /// <param name="log">ILogger</param>
        /// <param name="sourceStation">origination station name</param>
        /// <param name="destStation">Destination station name</param>
        /// <returns>Next three station timing with any delay</returns>
        private static string CallSeptaApi(ILogger log, string sourceStation, string destStation)
        {
            log.LogInformation($"Calling Septa API start. from {sourceStation} to {destStation}");
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", "NavneetFunctions");

                // septa train api
                var uri = $"http://www3.septa.org/hackathon/NextToArrive/{sourceStation}/{destStation}/3";

                HttpResponseMessage response = client.GetAsync(uri).Result;
                log.LogInformation($"Calling Septa API complete. Result {response?.StatusCode}");

                return response.Content.ReadAsStringAsync().Result;
            }

        }

        /// Resolve station name based on input query
        /// </summary>
        /// <param name="stationName">station name query</param>
        /// <returns>Resolved station name, if not resolved than default station name Default station name #0th Street Station </returns>
        private static string ResolveStationName(string stationName)
        {
            switch (stationName.Replace(" ", "").ToLower())
            {
                case "9st":
                case "9street":
                case string matchName when matchName.StartsWith("9th"):
                case "9":
                    return "9th St";
                case "30street":
                case "30st":
                case string matchName when matchName.StartsWith("30th"):
                case "30":
                    return "30th Street Station";
                case "49st":
                case "49street":
                case string matchName when matchName.StartsWith("49th"):
                case "49":
                    return "49th St";
                case "airportterminala":
                case "airporta":
                case "terminala":
                case "terma":
                    return "Airport Terminal A";
                case "airportterminalb":
                case "airportb":
                case "terminalb":
                case "termb":
                    return "Airport Terminal B";
                case "airportterminalc-d":
                case "airportterminalc":
                case "airportterminald":
                case "airportc":
                case "airportd":
                case "terminalc":
                case "terminald":
                case "termd":
                case "termc":
                    return "Airport Terminal C-D";
                case "airportterminale-f":
                case "airportterminale":
                case "airportterminalf":
                case "airporte-f":
                case "airporte":
                case "airportf":
                case "terminale-f":
                case "terminale":
                case "terminalf":
                    return "Airport Terminal E-F";
                case "allegheny":
                case string matchName when matchName.StartsWith("alleg"):
                    return "Allegheny";
                case "allenlane":
                case string matchName when matchName.StartsWith("alle"):
                    return "Allen Lane";
                case "ambler":
                case string matchName when matchName.StartsWith("am"):
                    return "Ambler";
                case "angora":
                case string matchName when matchName.StartsWith("an"):
                    return "Angora";
                case "ardmore":
                case string matchName when matchName.StartsWith("ardm"):
                    return "Ardmore";
                case "ardsley":
                case string matchName when matchName.StartsWith("ar"):
                    return "Ardsley";
                case "bala":
                case string matchName when matchName.StartsWith("ba"):
                    return "Bala";
                case "berwyn":
                case string matchName when matchName.StartsWith("ber"):
                    return "Berwyn";
                case "bethayres":
                case string matchName when matchName.StartsWith("bet"):
                    return "Bethayres";
                case "bridesburg":
                case string matchName when matchName.StartsWith("brid"):
                    return "Bridesburg";
                case "bristol":
                case string matchName when matchName.StartsWith("bri"):
                    return "Bristol";
                case "brynmawr":
                case string matchName when matchName.StartsWith("br"):
                    return "Bryn Mawr";
                case "carpenter":
                case string matchName when matchName.StartsWith("ca"):
                    return "Carpenter";
                case "chalfont":
                case string matchName when matchName.StartsWith("cha"):
                    return "Chalfont";
                case "cheltenavenue":
                case string matchName when matchName.StartsWith("cheltena"):
                    return "Chelten Avenue";
                case "cheltenham":
                case string matchName when matchName.StartsWith("chel"):
                    return "Cheltenham";
                case "chestertransportationcenter":
                case string matchName when matchName.StartsWith("cheste"):
                    return "Chester TC";
                case "chestnuthilleast":
                case "east":
                case string matchName when matchName.StartsWith("chestnuthilleast"):
                    return "Chestnut Hill East";
                case "chestnuthillwest":
                case "west":
                case string matchName when matchName.StartsWith("chestnut"):
                    return "Chestnut Hill West";
                case "churchmanscrossing,de":
                case string matchName when matchName.StartsWith("chu"):
                    return "Churchmans Crossing";
                case "claymont,de":
                case string matchName when matchName.StartsWith("cla"):
                    return "Claymont";
                case "clifton-aldan":
                case "aldan":
                case string matchName when matchName.StartsWith("cli"):
                    return "Clifton-Aldan";
                case "colmar":
                case string matchName when matchName.StartsWith("col"):
                    return "Colmar";
                case "conshohocken":
                case string matchName when matchName.StartsWith("con"):
                    return "Conshohocken";
                case "cornwellsheights":
                case string matchName when matchName.StartsWith("cor"):
                    return "Cornwells Heights";
                case "crestmont":
                case string matchName when matchName.StartsWith("cre"):
                    return "Crestmont";
                case "croydon":
                case string matchName when matchName.StartsWith("cro"):
                    return "Croydon";
                case "crumlynne":
                case string matchName when matchName.StartsWith("cr"):
                    return "Crum Lynne";
                case "curtispark":
                case string matchName when matchName.StartsWith("cu"):
                    return "Curtis Park";
                case "cynwyd":
                case string matchName when matchName.StartsWith("cy"):
                    return "Cynwyd";
                case "daylesford":
                case string matchName when matchName.StartsWith("day"):
                    return "Daylesford";
                case "darby":
                case string matchName when matchName.StartsWith("da"):
                    return "Darby";
                case "delawarevalleycollege":
                case "valley":
                case "college":
                case string matchName when matchName.StartsWith("da"):
                    return "Delaware Valley College";
                case "devon":
                case string matchName when matchName.StartsWith("de"):
                    return "Devon";
                case "downingtown":
                case string matchName when matchName.StartsWith("dow"):
                    return "Downingtown";
                case "doylestown":
                case string matchName when matchName.StartsWith("do"):
                    return "Doylestown";
                case "eastfalls":
                case "falls":
                case string matchName when matchName.StartsWith("eastf"):
                    return "East Falls";
                case "eastwick":
                case "wick":
                case string matchName when matchName.StartsWith("ea"):
                    return "Eastwick Station";
                case "eddington":
                case string matchName when matchName.StartsWith("eddi"):
                    return "Eddington";
                case "eddystone":
                case "stone":
                case string matchName when matchName.StartsWith("ed"):
                    return "Eddystone";
                case "elkinspark":
                case "park":
                case string matchName when matchName.StartsWith("elk"):
                    return "Elkins Park";
                case "elmstreet-norristown":
                case string matchName when matchName.StartsWith("elm"):
                    return "Elm St";
                case "elwyn":
                case string matchName when matchName.StartsWith("el"):
                    return "Elwyn Station";
                case "exton":
                case string matchName when matchName.StartsWith("ex"):
                    return "Exton";
                case "fernrocktransportationcenter":
                case "rock":
                case string matchName when matchName.StartsWith("fernr"):
                    return "Fern Rock TC";
                case "fernwood-yeadon":
                case "wood":
                case "yeadon":
                case string matchName when matchName.StartsWith("fe"):
                    return "Fernwood";
                case "folcroft":
                case string matchName when matchName.StartsWith("fol"):
                    return "Folcroft";
                case "foresthills":
                case "hills":
                case string matchName when matchName.StartsWith("for"):
                    return "Forest Hills";
                case "fortwashington":
                case "ft":
                case "Washington":
                case string matchName when matchName.StartsWith("fort"):
                    return "Ft Washington";
                case "fortuna":
                case string matchName when matchName.StartsWith("for"):
                    return "Fortuna";
                case "foxchase":
                case string matchName when matchName.StartsWith("fo"):
                    return "Fox Chase";
                case "germantown":
                case string matchName when matchName.StartsWith("fe"):
                    return "Germantown";
                case "gladstone":
                case string matchName when matchName.StartsWith("gla"):
                    return "Gladstone";
                case "glenolden":
                case "olden":
                case string matchName when matchName.StartsWith("gleno"):
                    return "Glenolden";
                case "glenside":
                case string matchName when matchName.StartsWith("gl"):
                    return "Glenside";
                case "gravers":
                case string matchName when matchName.StartsWith("gr"):
                    return "Gravers";
                case "gwyneddvalley":
                case string matchName when matchName.StartsWith("gw"):
                    return "Gwynedd Valley";
                case "hatboro":
                case string matchName when matchName.StartsWith("hat"):
                    return "Hatboro";
                case "haverford":
                case string matchName when matchName.StartsWith("ha"):
                    return "Haverford";
                case "highlandavenue":
                case string matchName when matchName.StartsWith("highlandav"):
                    return "Highland Ave";
                case "highland":
                case string matchName when matchName.StartsWith("hi"):
                    return "Highland";
                case "holmesburgjunction":
                case string matchName when matchName.StartsWith("ho"):
                    return "Holmesburg Jct";
                case "ivyridge":
                case string matchName when matchName.StartsWith("iv"):
                    return "Ivy Ridge";
                case "jeffersonstation(formerlymarketeast)":
                case string matchName when matchName.StartsWith("jef"):
                    return "Jefferson Station";
                case "jenkintown-wyncote":
                case string matchName when matchName.StartsWith("jen"):
                    return "Jenkintown-Wyncote";
                case "langhorne":
                case string matchName when matchName.StartsWith("lang"):
                    return "Langhorne";
                case "lansdale":
                case string matchName when matchName.StartsWith("lansda"):
                    return "Lansdale";
                case "lansdowne":
                case string matchName when matchName.StartsWith("lan"):
                    return "Lansdowne";
                case "lawndale":
                case string matchName when matchName.StartsWith("la"):
                    return "Lawndale";
                case "levittown":
                case string matchName when matchName.StartsWith("le"):
                    return "Levittown";
                case "linkbelt":
                case string matchName when matchName.StartsWith("li"):
                    return "Link Belt";
                case "mainstreet-norristown":
                case string matchName when matchName.StartsWith("mai"):
                    return "Main St";
                case "malvern":
                case string matchName when matchName.StartsWith("mal"):
                    return "Malvern";
                case "manayunk":
                case string matchName when matchName.StartsWith("man"):
                    return "Manayunk";
                case "marcushook":
                case string matchName when matchName.StartsWith("marc"):
                    return "Marcus Hook";
                case "marketeast(nowjeffersonstation)":
                case string matchName when matchName.StartsWith("ma"):
                    return "Market East";
                case "meadowbrook":
                case string matchName when matchName.StartsWith("mea"):
                    return "Meadowbrook";
                case "media":
                case string matchName when matchName.StartsWith("med"):
                    return "Media";
                case "melrosepark":
                case string matchName when matchName.StartsWith("mel"):
                    return "Melrose Park";
                case "merion":
                case string matchName when matchName.StartsWith("mer"):
                    return "Merion";
                case "miquon":
                case string matchName when matchName.StartsWith("mi"):
                    return "Miquon";
                case "morton":
                case string matchName when matchName.StartsWith("mor"):
                    return "Morton";
                case "moylan-rosevalley":
                case string matchName when matchName.StartsWith("moy"):
                    return "Moylan-Rose Valley";
                case "mt.airy":
                case "airy":
                case string matchName when matchName.StartsWith("mt"):
                    return "Mt Airy";
                case "narberth":
                case string matchName when matchName.StartsWith("na"):
                    return "Narberth";
                case "neshaminyfalls":
                case string matchName when matchName.StartsWith("na"):
                    return "Neshaminy Falls";
                case "newbritain":
                case "britain":
                case string matchName when matchName.StartsWith("newb"):
                    return "New Britain";
                case "newarkstation":
                case string matchName when matchName.StartsWith("ne"):
                    return "Newark";
                case "noblestation":
                case string matchName when matchName.StartsWith("nob"):
                    return "Noble";
                case "norristowntransportationcenter":
                case string matchName when matchName.StartsWith("norr"):
                    return "Norristown TC";
                case "northbroad":
                case string matchName when matchName.StartsWith("northbroad"):
                    return "North Broad St";
                case "northhills":
                case string matchName when matchName.StartsWith("northh"):
                    return "North Hills";
                case "northphiladelphia":
                case string matchName when matchName.StartsWith("northp"):
                    return "North Philadelphia";
                case "northwales":
                case "wales":
                case string matchName when matchName.StartsWith("northw"):
                    return "North Wales";
                case "norwood":
                case string matchName when matchName.StartsWith("no"):
                    return "Norwood";
                case "olney":
                case string matchName when matchName.StartsWith("ol"):
                    return "Olney";
                case "oreland":
                case string matchName when matchName.StartsWith("or"):
                    return "Oreland";
                case "overbrook":
                case string matchName when matchName.StartsWith("ov"):
                    return "Overbrook";
                case "paoli":
                case string matchName when matchName.StartsWith("pa"):
                    return "Paoli";
                case "penllyn":
                case string matchName when matchName.StartsWith("penl"):
                    return "Penllyn";
                case "pennbrook":
                case string matchName when matchName.StartsWith("pe"):
                    return "Pennbrook";
                case "philmont":
                case string matchName when matchName.StartsWith("ph"):
                    return "Philmont";
                case "primos":
                case string matchName when matchName.StartsWith("pri"):
                    return "Primos";
                case "prospectpark":
                case string matchName when matchName.StartsWith("pr"):
                    return "Prospect Park";
                case "queenlane":
                case string matchName when matchName.StartsWith("qu"):
                    return "Queen Lane";
                case "radnor":
                case string matchName when matchName.StartsWith("ra"):
                    return "Radnor";
                case "ridleypark":
                case string matchName when matchName.StartsWith("ri"):
                    return "Ridley Park";
                case "rosemont":
                case string matchName when matchName.StartsWith("rose"):
                    return "Rosemont";
                case "roslyn":
                case string matchName when matchName.StartsWith("ro"):
                    return "Roslyn";
                case "rydal":
                case string matchName when matchName.StartsWith("ryd"):
                    return "Rydal";
                case "ryers":
                case string matchName when matchName.StartsWith("ry"):
                    return "Ryers";
                case "secane":
                case string matchName when matchName.StartsWith("sec"):
                    return "Secane";
                case "sedgwick":
                case string matchName when matchName.StartsWith("se"):
                    return "Sedgwick";
                case "sharonhill":
                case string matchName when matchName.StartsWith("sh"):
                    return "Sharon Hill";
                case "somerton":
                case string matchName when matchName.StartsWith("so"):
                    return "Somerton";
                case "springmill":
                case string matchName when matchName.StartsWith("sp"):
                    return "Spring Mill";
                case "st.davids":
                case "stdavids":
                case string matchName when matchName.StartsWith("davi"):
                case string matchName2 when matchName2.StartsWith("st.d"):
                    return "St. Davids";
                case "st.martins":
                case "stmartins":
                case string matchName when matchName.StartsWith("st.m"):
                case string matchName2 when matchName2.StartsWith("mart"):
                    return "St. Martins";
                case "stenton":
                case string matchName when matchName.StartsWith("ste"):
                    return "Stenton";
                case "strafford":
                case string matchName when matchName.StartsWith("st"):
                    return "Strafford";
                case "suburbanstation":
                case string matchName when matchName.StartsWith("su"):
                    return "Suburban Station";
                case "swarthmore":
                case string matchName when matchName.StartsWith("sw"):
                    return "Swarthmore";
                case "tacony":
                case string matchName when matchName.StartsWith("ta"):
                    return "Tacony";
                case "templeuniversity":
                case string matchName when matchName.StartsWith("te"):
                    return "Temple U";
                case "thorndale":
                case string matchName when matchName.StartsWith("th"):
                    return "Thorndale";
                case "torresdale":
                case string matchName when matchName.StartsWith("to"):
                    return "Torresdale";
                case "trentontransitcenter":
                case string matchName when matchName.StartsWith("tren"):
                    return "Trenton";
                case "trevose":
                case string matchName when matchName.StartsWith("tre"):
                    return "Trevose";
                case "tulpehocken":
                case string matchName when matchName.StartsWith("tu"):
                    return "Tulpehocken";
                case "universitycity":
                case string matchName when matchName.StartsWith("un"):
                    return "University City";
                case "upsal":
                case string matchName when matchName.StartsWith("up"):
                    return "Upsal";
                case "villanova":
                case string matchName when matchName.StartsWith("vi"):
                    return "Villanova";
                case "wallingford":
                case string matchName when matchName.StartsWith("wal"):
                    return "Wallingford";
                case "warminster":
                case string matchName when matchName.StartsWith("war"):
                    return "Warminster";
                case "washingtonlane":
                case string matchName when matchName.StartsWith("was"):
                    return "Washington Lane";
                case "waynejunction":
                case string matchName when matchName.StartsWith("waynej"):
                    return "Wayne Jct";
                case "wayne":
                case string matchName when matchName.StartsWith("wa"):
                    return "Wayne Station";
                case "westtrenton,nj":
                case string matchName when matchName.StartsWith("we"):
                    return "West Trenton";
                case "whitford":
                case string matchName when matchName.StartsWith("wh"):
                    return "Whitford";
                case "willowgrove":
                case string matchName when matchName.StartsWith("will"):
                    return "Willow Grove";
                case "wilmington,de":
                case string matchName when matchName.StartsWith("wil"):
                    return "Wilmington";
                case "wissahickon":
                case string matchName when matchName.StartsWith("wiss"):
                    return "Wissahickon";
                case "wister":
                case string matchName when matchName.StartsWith("wi"):
                    return "Wister";
                case "woodbourne":
                case string matchName when matchName.StartsWith("wo"):
                    return "Woodbourne";
                case "wyndmoor":
                case string matchName when matchName.StartsWith("wynd"):
                    return "Wyndmoor";
                case "wynnefieldavenue":
                case string matchName when matchName.StartsWith("wynne"):
                    return "Wynnefield Avenue";
                case "wynnewood":
                case string matchName when matchName.StartsWith("wy"):
                    return "Wynnewood";
                case "yardley":
                case string matchName when matchName.StartsWith("ya"):
                    return "Yardley";
                default:
                    return "30th Street Station";
            }
        }
    }
}
