using FileHashTransfer.Common;
using System.Net;
using System.Text.Json;

namespace FileHashTransfer.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Сервер приема файлов ===");
            Console.WriteLine("Начинаю прослушивание на порту 8080...");

            var httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:8080/");
            httpListener.Start();

            while (true)
            {
                var context = await httpListener.GetContextAsync();
                _ = Task.Run(() => ProcessRequest(context));
            }
        }

        static async void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HttpMethod == "POST" && 
                    context.Request.Url.AbsolutePath == "/upload")
                {
                    await HandleFileUpload(context);
                }
                else
                {
                    context.Response.StatusCode = 404;
                    await context.Response.OutputStream.WriteAsync(
                        System.Text.Encoding.UTF8.GetBytes("Not Found"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                context.Response.StatusCode = 500;
            }
            finally
            {
                context.Response.Close();
            }
        }

        static async Task HandleFileUpload(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                string json = await reader.ReadToEndAsync();
                var fileData = JsonSerializer.Deserialize<FileTransferData>(json);

                if (fileData != null && fileData.FileData.Length > 0)
                {
                    // Проверка целостности файла
                    bool isIntegrityValid;
                    
                    if (fileData.Salt.Length > 0)
                    {
                        // 5.2: Проверка с использованием соли
                        isIntegrityValid = HashCalculator.VerifyHashWithSalt(
                            fileData.FileData, 
                            fileData.Salt, 
                            fileData.Hash
                        );
                    }
                    else
                    {
                        // Проверка без соли
                        string computedHash = HashCalculator.ComputeHash(fileData.FileData);
                        isIntegrityValid = computedHash == fileData.Hash;
                    }

                    // Сохранение файла
                    string savePath = Path.Combine("ReceivedFiles", fileData.FileName);
                    Directory.CreateDirectory("ReceivedFiles");
                    await File.WriteAllBytesAsync(savePath, fileData.FileData);

                    // Ответ клиенту
                    var response = new
                    {
                        Success = true,
                        FileName = fileData.FileName,
                        FileSize = fileData.FileSize,
                        IntegrityValid = isIntegrityValid,
                        Message = isIntegrityValid ? 
                            "Файл успешно получен и проверен" : 
                            "Ошибка целостности файла!"
                    };

                    string responseJson = JsonSerializer.Serialize(response);
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseJson);
                    
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(buffer);
                    
                    Console.WriteLine($"\nПолучен файл: {fileData.FileName}");
                    Console.WriteLine($"Размер: {fileData.FileSize} байт");
                    Console.WriteLine($"Целостность: {(isIntegrityValid ? "OK" : "НАРУШЕНА")}");
                }
            }
        }
    }
}