using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EmotionAnalyzer.Components;
using ScenaryBuilder.Components;

namespace  CsToPy
{

    // Define shared data structure
    public class MessageData
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double[] Values { get; set; } = Array.Empty<double>();
    }

    public class AiAnalytics{
      private bool isSending;
      private bool recived;
      private bool error;
      private string analytics;
      private int msgCount = 1;
      private StreamWriter writer;
      private Process process;
      public string errorMsg;
      public string pythonExe = "python3";
     

      private void processData(MessageData response){
        //Console.WriteLine($"Received JSON: ID={response.Id}, Message={response.Message}");
        
        Console.WriteLine(response.Message);
        HelperPanel.Instance.setMarkdown(response.Message);
      }
    
      public AiAnalytics(){

          // Get the path to the Python script
          string pythonScriptPath = Path.Combine("C:\\Users\\Lenovo\\Downloads\\nHak-final\\nHak-final\\", "agent.py");
          
          if (!File.Exists(pythonScriptPath))
          {
              Console.WriteLine($"Error: Python script not found at {pythonScriptPath}");
              return;
          }

          process = new Process
          {
              StartInfo = new ProcessStartInfo
              {
                  FileName = "python3",
                  Arguments = $"-u \"{pythonScriptPath}\" json",
                  UseShellExecute = false,
                  RedirectStandardInput = true,
                  RedirectStandardOutput = true,
                  RedirectStandardError = true,
                  CreateNoWindow = true,
                  StandardOutputEncoding = Encoding.UTF8,
                  StandardErrorEncoding = Encoding.UTF8
              }
          };

          process.OutputDataReceived += (sender, e) =>
          {
              if (!string.IsNullOrEmpty(e.Data) && e.Data.Trim().StartsWith("{"))
              {
                 
                  var response = JsonSerializer.Deserialize<MessageData>(e.Data);
                  processData(response);
                  Console.WriteLine($"[Python]: {e.Data}");
                    
              }
              else if (!string.IsNullOrEmpty(e.Data))
              {
                  Console.WriteLine($"[Python]: {e.Data}");
              }
          };

          process.ErrorDataReceived += (sender, e) =>
          {
              if (!string.IsNullOrEmpty(e.Data)){
                errorMsg = e.Data;
                error = true; 
                Console.WriteLine($"[Python Error]: {e.Data}");
              }
          };
          process.Start();
          process.BeginOutputReadLine();
          process.BeginErrorReadLine();
          writer = process.StandardInput;
      }
      
      public async Task requestAnalytics(string data){
        
        // Begin asynchronous reading of output and error
        var messageData = new MessageData {
          Id = msgCount, 
          Message = data,
          Timestamp = DateTime.Now
        };

        string json = JsonSerializer.Serialize(messageData);
        
        // 3. Write the JSON
        if (writer != null)
        {
            await writer.WriteLineAsync(json);
        }
      }


      ~AiAnalytics(){
        process.Kill();
      }
    
      public async Task waitForEnd(){
        await process.WaitForExitAsync();
      }
  }
}
