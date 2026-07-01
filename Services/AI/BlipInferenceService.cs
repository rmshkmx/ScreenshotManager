using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using ScreenshotManager.Helpers;

namespace ScreenshotManager.Services.AI;

/// <summary>
/// BLIP Image Captioning через ONNX Runtime.
/// Pipeline: Image → Preprocess → Vision Encoder → Text Decoder (autoregressive) → Caption.
/// Поддерживает полноценный Dispose для освобождения ОЗУ при отключении AI-именования.
/// </summary>
public sealed class BlipInferenceService : IDisposable
{
    private InferenceSession? _visionSession;
    private InferenceSession? _decoderSession;
    private Tokenizer? _tokenizer;
    private bool _disposed;
    private readonly object _lock = new();

    // BLIP model constants
    private const int MaxTokens = 30;
    private const int DecoderStartTokenId = 30522; // [DEC] token for BLIP
    private const int SepTokenId = 102;            // [SEP] token
    private const int PadTokenId = 0;              // [PAD] token

    /// <summary>Модель загружена и готова к inference.</summary>
    public bool IsModelLoaded
    {
        get
        {
            lock (_lock) { return _visionSession != null && _decoderSession != null && _tokenizer != null; }
        }
    }

    /// <summary>
    /// Загрузить ONNX модели и токенизатор.
    /// </summary>
    public void LoadModel()
    {
        lock (_lock)
        {
            if (IsModelLoaded) return;

            var modelsDir = ModelDownloadService.ModelsDir;

            var visionPath = Path.Combine(modelsDir, "vision_model.onnx");
            var decoderPath = Path.Combine(modelsDir, "decoder_model_merged.onnx");
            var vocabPath = Path.Combine(modelsDir, "vocab.txt");

            if (!File.Exists(visionPath) || !File.Exists(decoderPath) || !File.Exists(vocabPath))
            {
                throw new FileNotFoundException("BLIP model files not found. Download the model first.");
            }

            Debug.WriteLine("[BlipInference] Loading vision model...");
            var sessionOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };

            _visionSession = new InferenceSession(visionPath, sessionOptions);

            Debug.WriteLine("[BlipInference] Loading decoder model...");
            var decoderOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };
            _decoderSession = new InferenceSession(decoderPath, decoderOptions);

            Debug.WriteLine("[BlipInference] Loading tokenizer...");
            _tokenizer = WordPieceTokenizer.Create(vocabPath);

            Debug.WriteLine("[BlipInference] All models loaded successfully");
        }
    }

    /// <summary>
    /// Сгенерировать текстовое описание для изображения.
    /// Модель загружается на время inference и выгружается после для экономии ОЗУ.
    /// </summary>
    public async Task<string> GenerateCaptionAsync(Bitmap image)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!IsModelLoaded)
                {
                    Debug.WriteLine("[BlipInference] Loading model on-demand for inference...");
                    LoadModel();
                }

                return GenerateCaption(image);
            }
            finally
            {
                // Выгрузить модель сразу после inference для экономии ~1 ГБ ОЗУ
                Debug.WriteLine("[BlipInference] Unloading model after inference to save RAM...");
                UnloadModel();
            }
        });
    }

    /// <summary>
    /// Синхронная генерация описания.
    /// </summary>
    private string GenerateCaption(Bitmap image)
    {
        lock (_lock)
        {
            if (!IsModelLoaded)
                throw new InvalidOperationException("Model not loaded. Call LoadModel() first.");

            // 1. Препроцессинг изображения → float тензор [1, 3, 384, 384]
            var pixelValues = ImagePreprocessor.Preprocess(image);
            var pixelTensor = new DenseTensor<float>(pixelValues,
                new[] { 1, 3, ImagePreprocessor.TargetHeight, ImagePreprocessor.TargetWidth });

            // 2. Vision Encoder: pixel_values → last_hidden_state
            Debug.WriteLine("[BlipInference] Running vision encoder...");
            var visionInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("pixel_values", pixelTensor)
            };

            float[]? encoderHiddenStates;
            int[] encoderShape;

            using (var visionResults = _visionSession!.Run(visionInputs))
            {
                var hiddenState = visionResults.First();
                var hiddenTensor = hiddenState.AsTensor<float>();
                encoderShape = hiddenTensor.Dimensions.ToArray();
                encoderHiddenStates = hiddenTensor.ToArray();
            }

            // 3. Text Decoder: Autoregressive generation
            Debug.WriteLine("[BlipInference] Running text decoder...");
            var generatedIds = new List<long> { DecoderStartTokenId };

            // Create encoder attention mask (all 1s)
            var encoderSeqLen = encoderShape[1];
            var encoderAttentionMask = new long[encoderSeqLen];
            Array.Fill(encoderAttentionMask, 1L);

            for (int step = 0; step < MaxTokens; step++)
            {
                var inputIds = generatedIds.ToArray();
                var attentionMask = new long[inputIds.Length];
                Array.Fill(attentionMask, 1L);

                var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
                var encoderHiddenTensor = new DenseTensor<float>(encoderHiddenStates, encoderShape);

                var decoderInputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                    NamedOnnxValue.CreateFromTensor("encoder_hidden_states", encoderHiddenTensor)
                };

                if (_decoderSession!.InputMetadata.ContainsKey("attention_mask"))
                {
                    var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, inputIds.Length });
                    decoderInputs.Add(NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor));
                }

                if (_decoderSession!.InputMetadata.ContainsKey("encoder_attention_mask"))
                {
                    var encoderAttentionTensor = new DenseTensor<long>(encoderAttentionMask, new[] { 1, encoderSeqLen });
                    decoderInputs.Add(NamedOnnxValue.CreateFromTensor("encoder_attention_mask", encoderAttentionTensor));
                }

                using var decoderResults = _decoderSession!.Run(decoderInputs);

                // Get logits [1, seq_len, vocab_size]
                var logitsTensor = decoderResults.First().AsTensor<float>();
                var vocabSize = logitsTensor.Dimensions[2];
                var seqLen = logitsTensor.Dimensions[1];

                // Argmax на последний токен
                var nextTokenId = ArgmaxLastToken(logitsTensor, seqLen, vocabSize);

                if (nextTokenId == SepTokenId || nextTokenId == PadTokenId)
                    break;

                generatedIds.Add(nextTokenId);
            }

            // 4. Декодировать токены в текст
            // Remove the initial [DEC] token
            var tokensToDecode = generatedIds.Skip(1).Select(id => (int)id);
            var caption = _tokenizer!.Decode(tokensToDecode);

            Debug.WriteLine($"[BlipInference] Caption: {caption}");
            return caption?.Trim() ?? string.Empty;
        }
    }

    /// <summary>
    /// Найти индекс максимального значения в последней позиции последовательности.
    /// </summary>
    private static long ArgmaxLastToken(Tensor<float> logits, int seqLen, int vocabSize)
    {
        float maxVal = float.MinValue;
        long maxIdx = 0;

        int lastPos = seqLen - 1;

        for (int v = 0; v < vocabSize; v++)
        {
            var val = logits[0, lastPos, v];
            if (val > maxVal)
            {
                maxVal = val;
                maxIdx = v;
            }
        }

        return maxIdx;
    }

    /// <summary>
    /// Полностью выгрузить модель из памяти, освободив ОЗУ.
    /// </summary>
    public void UnloadModel()
    {
        lock (_lock)
        {
            Debug.WriteLine("[BlipInference] Unloading models...");

            _visionSession?.Dispose();
            _visionSession = null;

            _decoderSession?.Dispose();
            _decoderSession = null;

            // Tokenizer не реализует IDisposable — просто обнуляем ссылку
            _tokenizer = null;

            // Принудительная сборка мусора для освобождения нативной памяти
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Debug.WriteLine("[BlipInference] Models unloaded, memory released");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnloadModel();
    }
}
