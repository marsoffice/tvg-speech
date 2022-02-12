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
using Newtonsoft.Json;
using MarsOffice.Tvg.Speech.Entities;
using Newtonsoft.Json.Serialization;
using System.Linq;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;

namespace MarsOffice.Tvg.Speech
{
    public class RequestSpeechConsumer
    {
        private readonly IConfiguration _config;
        private readonly CloudBlobClient _blobClient;

        public RequestSpeechConsumer(IConfiguration config)
        {
            _config = config;
            var cloudStorageAccount = CloudStorageAccount.Parse(_config["localsaconnectionstring"]);
            _blobClient = cloudStorageAccount.CreateCloudBlobClient();
        }

        [FunctionName("RequestSpeechConsumer")]
        public async Task Run(
            [QueueTrigger("request-speech", Connection = "localsaconnectionstring")] RequestSpeech request,
            [Queue("speech-result", Connection = "localsaconnectionstring")] IAsyncCollector<SpeechResult> speechResultQueue,

            ILogger log)
        {
            string tempFolderName = null;
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
                synthesizer.Properties.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, $"{request.SpeechPauseBeforeInMillis ?? 1000}");
                synthesizer.Properties.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, $"{request.SpeechPauseAfterInMillis ?? 1000}");

                tempFolderName = Path.GetTempPath() + Guid.NewGuid().ToString();
                Directory.CreateDirectory(tempFolderName);

                var i = 0;
                var mp3Files = new List<string>();
                var durations = new List<long>();

                foreach (var sentence in request.Sentences)
                {
                    var result = await synthesizer.SpeakTextAsync(sentence);
                    var fileName = tempFolderName + "/" + $"{i}.mp3";
                    mp3Files.Add($"{i}.mp3");
                    await File.WriteAllBytesAsync(fileName, result.AudioData);

                    var psiFile = new ProcessStartInfo
                    {
                        FileName = _config["ffprobepath"],
                        Arguments = $"-i {i}.mp3 -v quiet -print_format json -show_format -hide_banner",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        WorkingDirectory = tempFolderName
                    };

                    var processFile = Process.Start(psiFile);
                    var stdOutFile = processFile.StandardOutput.ReadToEnd();
                    processFile.WaitForExit((int)TimeSpan.FromSeconds(60).TotalMilliseconds);

                    if (string.IsNullOrEmpty(stdOutFile) || processFile.ExitCode != 0)
                    {
                        throw new Exception("Unable to read TTS audio output info");
                    }

                    var ffProbeResponseFile = JsonConvert.DeserializeObject<FfProbeResponse>(stdOutFile, new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        NullValueHandling = NullValueHandling.Ignore
                    });
                    durations.Add((long)Math.Round(ffProbeResponseFile.Format.Duration * 1000));
                    i++;
                }

                var mp3Chunks = string.Join("|", mp3Files);
                var psi = new ProcessStartInfo
                {
                    FileName = _config["ffmpegpath"],
                    Arguments = $"-i \"concat:{mp3Chunks}\" -v quiet -hide_banner -acodec copy output.mp3",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = tempFolderName
                };

                var process = Process.Start(psi);
                process.WaitForExit((int)TimeSpan.FromSeconds(60).TotalMilliseconds);

                var outputExists = File.Exists(tempFolderName + "/output.mp3");
                if (process.ExitCode != 0 || !outputExists)
                {
                    throw new Exception("Unable to merge TTS audio files");
                }

                var blobContainerReference = _blobClient.GetContainerReference("jobsdata");
#if DEBUG
                await blobContainerReference.CreateIfNotExistsAsync();
#endif
                var blobReference = blobContainerReference.GetBlockBlobReference($"{request.JobId}/tts.mp3");
           
                await blobReference.UploadFromFileAsync(tempFolderName + "/output.mp3");

                blobReference.Metadata.Add("IndividualDurationsInMillis", string.Join(",", durations));
                blobReference.Metadata.Add("TotalDurationInMillis", durations.Sum().ToString());
                await blobReference.SetMetadataAsync();

                await speechResultQueue.AddAsync(new SpeechResult
                {
                    Success = true,
                    JobId = request.JobId,
                    VideoId = request.VideoId,
                    UserEmail = request.UserEmail,
                    UserId = request.UserId,
                    IndividualDurationsInMillis = durations,
                    TotalDurationInMillis = durations.Sum(),
                    FileLink = $"jobsdata/{request.JobId}/tts.mp3"
                });
                await speechResultQueue.FlushAsync();
            }
            catch (Exception e)
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
            finally
            {
                try
                {
                    Directory.Delete(tempFolderName, true);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Temp folder deletion failed");
                }
            }
        }
    }
}
