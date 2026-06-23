using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace ScreenshotManager.Services.AI;

/// <summary>
/// Скачивание BLIP ONNX модели с HuggingFace.
/// Файлы: vision_model.onnx, text_decoder_model_merged.onnx, vocab.txt
/// </summary>
public sealed class ModelDownloadService
{
    // Base URL для файлов модели на HuggingFace
    private const string HfBaseUrl = "https://huggingface.co/ningpp/blip-image-captioning-base-ONNX/resolve/main/";
    private const string HfTokenizerUrl = "https://huggingface.co/ningpp/blip-image-captioning-base-ONNX/resolve/main/";

    // Файлы модели для скачивания
    public static readonly (string Url, string FileName, string Description)[] ModelFiles =
    {
        (HfBaseUrl + "blip_vision_encoder.onnx", "vision_model.onnx", "Vision Encoder (~350 MB)"),
        (HfBaseUrl + "blip_text_decoder.onnx", "decoder_model_merged.onnx", "Text Decoder (~300 MB)"),
        (HfTokenizerUrl + "vocab.txt", "vocab.txt", "Vocabulary (~230 KB)"),
        (HfTokenizerUrl + "tokenizer_config.json", "tokenizer_config.json", "Tokenizer Config (~1 KB)"),
        (HfTokenizerUrl + "config.json", "config.json", "Model Config (~5 KB)")
    };

    /// <summary>Папка хранения моделей.</summary>
    public static string ModelsDir =>
        Path.Combine(SettingsService.AppDataDir, "Models", "blip-captioning-base");

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private CancellationTokenSource? _cts;

    /// <summary>Прогресс скачивания текущего файла (0.0 — 1.0).</summary>
    public event Action<double, string>? ProgressChanged;

    /// <summary>Скачивание завершено.</summary>
    public event Action<bool, string?>? DownloadCompleted;

    public ModelDownloadService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ScreenshotManager/1.0");
        _httpClient.Timeout = TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Проверить, все ли файлы модели скачаны.
    /// </summary>
    public bool IsModelDownloaded()
    {
        return ModelFiles.All(f => File.Exists(Path.Combine(ModelsDir, f.FileName)));
    }

    /// <summary>
    /// Скачать все файлы модели.
    /// </summary>
    public async Task DownloadModelAsync()
    {
        _cts = new CancellationTokenSource();

        try
        {
            Directory.CreateDirectory(ModelsDir);

            for (int i = 0; i < ModelFiles.Length; i++)
            {
                var (url, fileName, description) = ModelFiles[i];
                var filePath = Path.Combine(ModelsDir, fileName);

                // Пропустить уже скачанные файлы
                if (File.Exists(filePath))
                {
                    Debug.WriteLine($"[ModelDownload] Skipping (exists): {fileName}");
                    continue;
                }

                Debug.WriteLine($"[ModelDownload] Downloading: {description} from {url}");
                ProgressChanged?.Invoke(0, description);

                await DownloadFileAsync(url, filePath, description, _cts.Token);
            }

            DownloadCompleted?.Invoke(true, null);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[ModelDownload] Cancelled");
            DownloadCompleted?.Invoke(false, "Download cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModelDownload] Error: {ex.Message}");
            DownloadCompleted?.Invoke(false, ex.Message);
        }
    }

    /// <summary>
    /// Скачать один файл с прогресс-репортингом.
    /// </summary>
    private async Task DownloadFileAsync(string url, string filePath, string description, CancellationToken ct)
    {
        var currentUrl = url;
        HttpResponseMessage response = null!;
        int maxRedirects = 5;

        for (int i = 0; i < maxRedirects; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, currentUrl);
            
            // Only send auth token to huggingface.co, NOT to AWS S3/CloudFront redirects
            if (currentUrl.StartsWith("https://huggingface.co/"))
            {
                var token = _settingsService.Settings.HfToken?.Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            }

            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            // If we get 401/403 and we sent a token, try again without the token (fallback for public models)
            if ((response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden) &&
                request.Headers.Authorization != null)
            {
                Debug.WriteLine("[ModelDownload] Auth failed with token. Retrying without token...");
                response.Dispose();
                
                var retryRequest = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                response = await _httpClient.SendAsync(retryRequest, HttpCompletionOption.ResponseHeadersRead, ct);
            }

            // Handle manual redirects
            var statusCode = (int)response.StatusCode;
            if (statusCode >= 300 && statusCode <= 399)
            {
                var redirectUrl = response.Headers.Location?.ToString();
                if (redirectUrl != null)
                {
                    if (!redirectUrl.StartsWith("http"))
                    {
                        redirectUrl = new Uri(new Uri(currentUrl), redirectUrl).ToString();
                    }
                    currentUrl = redirectUrl;
                    response.Dispose();
                    continue;
                }
            }
            break;
        }

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        long downloadedBytes = 0;

        var tempPath = filePath + ".tmp";

        await using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
        await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
        {
            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    var progress = (double)downloadedBytes / totalBytes;
                    ProgressChanged?.Invoke(progress, description);
                }
            }
        }

        // Переименовать tmp → окончательное имя (атомарная операция)
        File.Move(tempPath, filePath, overwrite: true);
        Debug.WriteLine($"[ModelDownload] Completed: {description} ({downloadedBytes:N0} bytes)");
    }

    /// <summary>
    /// Отменить текущее скачивание.
    /// </summary>
    public void CancelDownload()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// Удалить все файлы модели.
    /// </summary>
    public void DeleteModel()
    {
        try
        {
            if (Directory.Exists(ModelsDir))
            {
                Directory.Delete(ModelsDir, recursive: true);
                Debug.WriteLine("[ModelDownload] Model files deleted");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModelDownload] Delete error: {ex.Message}");
        }
    }
}
