using System;
using System.Net.Http;
using System.Threading.Tasks;
using MarsOffice.Tvg.Speech.Abstractions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MarsOffice.Tvg.Speech
{
    public class RequestSpeechConsumer
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public RequestSpeechConsumer(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClient = httpClientFactory.CreateClient();
        }

        [FunctionName("RequestSpeechConsumer")]
        public async Task Run(
            [QueueTrigger("request-speech", Connection = "localsaconnectionstring")]RequestSpeech request,
            [Queue("speech-result", Connection = "localsaconnectionstring")] IAsyncCollector<SpeechResult> speechResultQueue,
            ILogger log)
        {
            try
            {

            } catch (Exception e)
            {
                log.LogError(e, "Function threw an exception");
                await speechResultQueue.AddAsync(new SpeechResult
                {
                    Error = e.Message,
                    Success = false,
                    JobId = request.JobId,
                    VideoId = request.VideoId,
                    UserEmail = request.UserEmail,
                    UserId = request.UserId
                });
                await speechResultQueue.FlushAsync();
            }
        }
    }
}
