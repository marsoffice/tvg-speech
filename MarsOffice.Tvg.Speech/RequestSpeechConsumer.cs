using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MarsOffice.Tvg.Speech.Abstractions;
using MarsOffice.Tvg.Speech.Entities;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Tvg.Speech
{
    public class RequestSpeechConsumer
    {
        private readonly IConfiguration _config;
        private readonly CloudBlobClient _blobClient;
        private readonly HttpClient _httpClient;
        private const string _audioFormat = "audio-48khz-192kbitrate-mono-mp3";

        public RequestSpeechConsumer(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _config = config;
            var cloudStorageAccount = CloudStorageAccount.Parse(_config["localsaconnectionstring"]);
            _blobClient = cloudStorageAccount.CreateCloudBlobClient();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _config["speechkey"]);
            _httpClient.DefaultRequestHeaders.Add("X-Microsoft-OutputFormat", _audioFormat);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", ".NetCore");
        }

        [FunctionName("RequestSpeechConsumer")]
        public async Task Run(
            [QueueTrigger("request-speech", Connection = "localsaconnectionstring")] RequestSpeech request,
            [Queue("speech-result", Connection = "localsaconnectionstring")] IAsyncCollector<SpeechResult> speechResultQueue,

            ILogger log)
        {
            var baseUrl = $"https://{_config["location"].Replace(" ", "").ToLower()}.tts.speech.microsoft.com/cognitiveservices";
            string tempFolderName = null;
            try
            {
                var voicesResponse = await _httpClient.GetAsync(baseUrl + "/voices/list");
                voicesResponse.EnsureSuccessStatusCode();
                var voicesJson = await voicesResponse.Content.ReadAsStringAsync();
                var voices = JsonConvert.DeserializeObject<IEnumerable<AzureTtsVoice>>(voicesJson, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });

                var voice = voices.Where(x => x.Locale == (request.SpeechLanguage ?? "en-US")).First().ShortName;

                tempFolderName = Path.GetTempPath() + Guid.NewGuid().ToString();
                Directory.CreateDirectory(tempFolderName);

                var i = 0;
                var mp3Files = new List<string>();
                var durations = new List<long>();

                foreach (var sentence in request.Sentences)
                {
                    var httpResponse = await _httpClient.PostAsync(baseUrl + "/v1", new StringContent(
                        $"<speak version='1.0' xml:lang='{request.SpeechLanguage ?? "en-US"}'><voice name='{request.SpeechType ?? voice}'><prosody rate='{request.SpeechSpeed ?? 0}%' pitch='{request.SpeechPitch ?? 0}%'>{sentence}</prosody></voice></speak>"
                        , Encoding.UTF8, "application/ssml+xml"));
                    httpResponse.EnsureSuccessStatusCode();
                    using var audioStream = await httpResponse.Content.ReadAsStreamAsync();

                    var fileName = tempFolderName + "/" + $"{i}.mp3";
                    mp3Files.Add($"{i}.mp3");
                    using var fileStream = File.OpenWrite(fileName);
                    await audioStream.CopyToAsync(fileStream);
                    fileStream.Close();
                    audioStream.Close();
                    
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
                var blobReference = blobContainerReference.GetBlockBlobReference($"{request.VideoId}/tts.mp3");

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
                    FileLink = $"jobsdata/{request.VideoId}/tts.mp3"
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
