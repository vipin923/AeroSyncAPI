using AeroSyncAPI.Models;
using System.Text.Json;
using Tesseract;

namespace AeroSyncAPI.Services
{
    public class SmartItineraryService
    {
        private readonly SmartIntentService _aiEngine;
        private readonly string _tessDataPath;
        public SmartItineraryService(SmartIntentService aiEngine, IWebHostEnvironment env)
        {
            _aiEngine = aiEngine;

            // Points to the tessdata folder you created in Step 1
            _tessDataPath = Path.Combine(env.ContentRootPath, "tessdata");
        }
        public async Task<FlightItinerary?> ProcessScreenshotAsync(Stream imageStream)
        {
            Console.WriteLine("\n🚀 [AeroSync API] ProcessScreenshotAsync started!");

            if (imageStream == null)
            {
                Console.WriteLine("❌ [Error] Received a null image stream from the frontend.");
                throw new ArgumentNullException(nameof(imageStream), "Image stream cannot be null.");
            }

            try
            {
                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream);
                byte[] imageBytes = memoryStream.ToArray();

                Console.WriteLine($"📸 [AeroSync API] Image successfully converted to byte array. Size: {imageBytes.Length} bytes.");

                // 1. Run the OCR engine
                Console.WriteLine("🔍 [AeroSync API] Running Tesseract OCR text extraction...");
                string rawText = ExtractTextWithOcr(imageBytes);

                if (string.IsNullOrWhiteSpace(rawText))
                {
                    Console.WriteLine("❌ [Error] Tesseract OCR returned empty text or failed to read the image layout.");
                    throw new Exception("Could not detect any text in the uploaded image.");
                }

                Console.WriteLine($"✅ [AeroSync API] OCR Extraction successful! Raw text found:\n---\n{rawText}\n---");

                // 2. Query the Phi-3 Engine
                Console.WriteLine("🤖 [AeroSync API] Sending raw text payload to Phi-3 LLM...");
                string jsonResult = await _aiEngine.ParseItineraryAsync(rawText);

                if (string.IsNullOrWhiteSpace(jsonResult))
                {
                    Console.WriteLine("❌ [Error] Phi-3 engine returned an empty or null response string.");
                    throw new Exception("AI Engine failed to return a response.");
                }

                Console.WriteLine($"✅ [AeroSync API] Raw LLM response received:\n---\n{jsonResult}\n---");

                // 3. Extract JSON Brackets
                int startIndex = jsonResult.IndexOf('{');
                int endIndex = jsonResult.LastIndexOf('}');

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    jsonResult = jsonResult.Substring(startIndex, endIndex - startIndex + 1);
                }
                else
                {
                    Console.WriteLine("❌ [Error] Could not locate standard JSON brackets { } in the LLM text output.");
                    throw new Exception($"AI failed to generate a structurally valid JSON object. Raw output: {jsonResult}");
                }

                string cleanJson = CleanLlmJsonOutput(jsonResult);
                Console.WriteLine($"🧹 [AeroSync API] Sanitized JSON target string:\n{cleanJson}");

                // 4. Parse the Model
                var itinerary = JsonSerializer.Deserialize<FlightItinerary>(cleanJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Console.WriteLine($"🎉 [AeroSync API] Data parsed successfully! Flight Number: {itinerary?.FlightNumber}, Gate: {itinerary?.Gate}");
                return itinerary;
            }
            catch (Exception ex)
            {
                // 🚨 CRITICAL: Look at your console window to see this print!
                Console.WriteLine($"\n💥 [CRITICAL CRASH] Exception inside ProcessScreenshotAsync: {ex.Message}");
                Console.WriteLine($"Details: {ex.StackTrace}\n");
                throw;
            }
        }
        private string CleanLlmJsonOutput(string rawAiOutput)
        {
            if (string.IsNullOrWhiteSpace(rawAiOutput)) return "{}";

            // 1. Find where the very first JSON block begins
            int startIndex = rawAiOutput.IndexOf('{');
            if (startIndex < 0) return "{}";

            // 2. Clear out any text preceding the first bracket
            string structuralText = rawAiOutput.Substring(startIndex);

            // 3. Track bracket depth to find where the FIRST JSON object actually ends
            int bracketCount = 0;
            int endIndex = -1;

            for (int i = 0; i < structuralText.Length; i++)
            {
                if (structuralText[i] == '{')
                {
                    bracketCount++;
                }
                else if (structuralText[i] == '}')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        // We found the exact matching closing bracket for the first object!
                        endIndex = i;
                        break;
                    }
                }
            }

            // 4. Extract ONLY that single, isolated JSON block
            if (endIndex >= 0)
            {
                return structuralText.Substring(0, endIndex + 1);
            }

            // Fallback to basic trimming if bracket matching logic couldn't resolve
            int basicEndIndex = rawAiOutput.LastIndexOf('}');
            if (basicEndIndex > startIndex)
            {
                return rawAiOutput.Substring(startIndex, basicEndIndex - startIndex + 1);
            }

            return "{}";
        }
        private string ExtractTextWithOcr(byte[] imageBytes)
        {
            // Initialize the Tesseract Engine
            using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);

            // Load the image bytes into Tesseract's specific Pix format
            using var img = Pix.LoadFromMemory(imageBytes);

            // Process the image and rip the text
            using var page = engine.Process(img);

            return page.GetText();
        }
    }
}
