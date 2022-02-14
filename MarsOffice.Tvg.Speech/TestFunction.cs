using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MarsOffice.Tvg.Speech
{
    public class TestFunction
    {
        [FunctionName("TestFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/speech/test")] HttpRequest req,
            ILogger log
            )
        {
            
            try {
                string cmd = req.Query["cmd"];

                var psiFile = new ProcessStartInfo
                {
                    Arguments = cmd,
                    FileName = "/bin/bash"
                };
                await Task.CompletedTask;

                var processFile = Process.Start(psiFile);
                var stdOutFile = processFile.StandardOutput.ReadToEnd();
                processFile.WaitForExit((int)TimeSpan.FromSeconds(60).TotalMilliseconds);

                return new OkObjectResult(stdOutFile);
            } catch (Exception e) {
                log.LogError(e, "Test failed");
                return new BadRequestObjectResult(new {e.Message, e.Data});
            }
        }
    }
}
