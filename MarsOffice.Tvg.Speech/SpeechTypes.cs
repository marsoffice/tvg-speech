using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MarsOffice.Microfunction;
using MarsOffice.Tvg.Speech.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Tvg.Speech
{
    public class SpeechTypes
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly string _baseUrl;

        public SpeechTypes(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _config["speechkey"]);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", ".NetCore");
            _baseUrl = $"https://{_config["location"].Replace(" ", "").ToLower()}.tts.speech.microsoft.com/cognitiveservices";
        }

        [FunctionName("GetAllSpeechTypes")]
        public async Task<IActionResult> GetAllSpeechTypes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/speech/getAllSpeechTypes/{locale}")] HttpRequest req,
            ILogger log
            )
        {
            try
            {
                var locale = req.RouteValues["locale"].ToString();
                if (string.IsNullOrEmpty(locale))
                {
                    throw new Exception("Invalid locale");
                }
                var voicesResponse = await _httpClient.GetAsync(_baseUrl + "/voices/list");
                voicesResponse.EnsureSuccessStatusCode();
                var voicesJson = await voicesResponse.Content.ReadAsStringAsync();
                var voices = JsonConvert.DeserializeObject<IEnumerable<AzureTtsVoice>>(voicesJson, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                return new OkObjectResult(
                    voices.Where(x => x.Locale.ToLower() == locale.ToLower()).Select(x => x.ShortName).Distinct().OrderBy(x => x).ToList()
                    );
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("GetAllSpeechTypesNoLocale")]
        public async Task<IActionResult> GetAllSpeechTypesNoLocale(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/speech/getAllSpeechTypes")] HttpRequest req,
            ILogger log
            )
        {
            try
            {
                var voicesResponse = await _httpClient.GetAsync(_baseUrl + "/voices/list");
                voicesResponse.EnsureSuccessStatusCode();
                var voicesJson = await voicesResponse.Content.ReadAsStringAsync();
                var voices = JsonConvert.DeserializeObject<IEnumerable<AzureTtsVoice>>(voicesJson, new JsonSerializerSettings { 
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                return new OkObjectResult(
                    voices.Select(x => x.ShortName).Distinct().OrderBy(x => x).ToList()
                    );
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("GetAllSpeechLanguages")]
        public async Task<IActionResult> GetAllSpeechLanguages(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/speech/getAllSpeechLanguages")] HttpRequest req,
            ILogger log
            )
        {
            try
            {
                var voicesResponse = await _httpClient.GetAsync(_baseUrl + "/voices/list");
                voicesResponse.EnsureSuccessStatusCode();
                var voicesJson = await voicesResponse.Content.ReadAsStringAsync();
                var voices = JsonConvert.DeserializeObject<IEnumerable<AzureTtsVoice>>(voicesJson, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                return new OkObjectResult(
                    voices.Select(x => x.Locale).Distinct().OrderBy(x => x).ToList()
                    );
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }
    }
}