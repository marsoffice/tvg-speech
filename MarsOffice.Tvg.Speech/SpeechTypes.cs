using System;
using System.Linq;
using System.Threading.Tasks;
using MarsOffice.Microfunction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MarsOffice.Tvg.Speech
{
    public class SpeechTypes
    {
        private readonly SpeechSynthesizer _synth;

        public SpeechTypes(IConfiguration config)
        {
            _synth = new SpeechSynthesizer(SpeechConfig.FromSubscription(config["speechkey"], config["location"].Replace(" ", "").ToLower()), null);
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
                var voices = await _synth.GetVoicesAsync(locale);
                return new OkObjectResult(
                    voices.Voices.Select(x => x.ShortName).Distinct().OrderBy(x => x).ToList()
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
                var voices = await _synth.GetVoicesAsync();
                return new OkObjectResult(
                    voices.Voices.Select(x => x.ShortName).Distinct().OrderBy(x => x).ToList()
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
                var voices = await _synth.GetVoicesAsync();
                return new OkObjectResult(voices.Voices.Select(x => x.Locale).Distinct().OrderBy(x => x).ToList());
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }
    }
}