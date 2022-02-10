using System;
using System.Threading.Tasks;
using MarsOffice.Tvg.Speech.Abstractions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace MarsOffice.Tvg.Speech
{
    public class RequestSpeechConsumer
    {
        private readonly IConfiguration _config;

        public RequestSpeechConsumer(IConfiguration config)
        {
            _config = config;
        }

        [FunctionName("RequestSpeechConsumer")]
        public async Task Run(
            [QueueTrigger("request-speech", Connection = "localsaconnectionstring")]RequestSpeech request,
            [Queue("speech-result", Connection = "localsaconnectionstring")] IAsyncCollector<SpeechResult> speechResultQueue,
            
            ILogger log)
        {
            try
            {
                var speechConfig = SpeechConfig.FromSubscription(_config["speechkey"], _config["location"].Replace(" ", "").ToLower());
                speechConfig.SpeechSynthesisLanguage = request.SpeechLanguage ?? "en-US";
                if (!string.IsNullOrEmpty(request.SpeechType))
                {
                    speechConfig.SpeechSynthesisVoiceName = request.SpeechType;
                }
                speechConfig.SetProfanity(ProfanityOption.Masked);
                speechConfig.OutputFormat = OutputFormat.Simple;
                speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio48Khz192KBitRateMonoMp3);

                using var synthesizer = new SpeechSynthesizer(speechConfig, null);
                var tempFolderName = Path.GetTempPath() + Guid.NewGuid().ToString();
                Directory.CreateDirectory(tempFolderName);

                var i = 0;
                var mp3Files = new List<string>();

                foreach (var sentence in request.Sentences)
                {
                    var result = await synthesizer.SpeakTextAsync(sentence);
                    var fileName = $"{i}.mp3";
                    mp3Files.Add(fileName);
                    await File.WriteAllBytesAsync(tempFolderName + "/" + $"{i}.mp3", result.AudioData);
                    i++;
                }

                var mp3Chunks = string.Join(" | ", mp3Files);
                var psi = new ProcessStartInfo
                {
                    FileName = _config["ffmpegpath"],
                    Arguments = $"-i \"concat:{mp3Chunks}\" -acodec copy output.mp3",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = tempFolderName
                };

                var process = Process.Start(psi);
                process.WaitForExit((int)TimeSpan.FromSeconds(60).TotalMilliseconds);

                try
                {
                    Directory.Delete(tempFolderName, true);
                } catch (Exception ex)
                {
                    log.LogError(ex, "Temp folder deletion failed");
                }
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
