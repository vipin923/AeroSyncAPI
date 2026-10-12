using LLama;
using LLama.Common;
using LLama.Sampling;

namespace AeroSyncAPI.Services
{
    public class SmartIntentService
    {
        private readonly LLamaWeights _weights;
        private readonly ModelParams _parameters;

        public SmartIntentService(LLamaWeights weights, ModelParams parameters)
        {
            _weights = weights;
            _parameters = parameters;
        }

        public async Task<string> ParseIntentAsync(string input)
        {
            // Create a lightweight context specifically for this HTTP request
            using var context = _weights.CreateContext(_parameters);
            var executor = new InstructExecutor(context);

            var inferenceParams = new InferenceParams()
            {
                MaxTokens = 150,
                AntiPrompts = new List<string> { "<|end|>", "<|user|>" },
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = 0.1f
                }
            };

            string prompt = $@"<|system|>
You are an intent extraction engine for a travel app. Analyze the passenger's message.
Return ONLY a raw JSON object with no markdown formatting.
Format: {{ ""Categories"": [""Dining"", ""Networking"", ""Kill Time""], ""Duration"": ""4 hours"" }}<|end|>
<|user|>
{input}<|end|>
<|assistant|>";

            string jsonResponse = "";

            await foreach (var text in executor.InferAsync(prompt, inferenceParams))
            {
                jsonResponse += text;
            }

            // 🚀 1. Chop off the hallucination! Split the string at the first sign of an AI tag
            if (jsonResponse.Contains("<|assistant|>"))
            {
                jsonResponse = jsonResponse.Split("<|assistant|>")[0];
            }
            if (jsonResponse.Contains("<|end|>"))
            {
                jsonResponse = jsonResponse.Split("<|end|>")[0];
            }

            // 🚀 2. Now that the extra garbage is gone, safely grab the first JSON object
            int startIndex = jsonResponse.IndexOf('{');
            int endIndex = jsonResponse.LastIndexOf('}');

            if (startIndex >= 0 && endIndex > startIndex)
            {
                return jsonResponse.Substring(startIndex, endIndex - startIndex + 1).Trim();
            }

            return jsonResponse.Trim();
        }

        public async Task<string> ParseItineraryAsync(string rawText)
        {
            using var context = _weights.CreateContext(_parameters);
            var executor = new InstructExecutor(context);

            var inferenceParams = new InferenceParams()
            {
                MaxTokens = 150,
                // 🚀 THE FIX: Temperature is now inside the DefaultSamplingPipeline
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = 0.0f
                }
            };
            // 🚀 The Magic Prompt: Forces the AI to format the messy text into a rigid JSON object
            string prompt = $@"<|system|>
You are a highly precise travel itinerary extraction engine. 
Analyze the raw OCR text and extract the flight details. 

STRICT EXTRACTION RULES:
1. 'DepartureTerminal' is usually a single digit or short alphanumeric (e.g., '2', 'T3'). It is NEVER a 3-letter airport code like 'NAG' or 'DEL'.
2. If you see a number followed by a gate (e.g., '2 A14'), the '2' is the Terminal and 'A14' is the Gate. Separate them!
3. 'DepartureTime' can be inferred from 'Boarding Time' if departure is not explicitly stated.

You must respond ONLY with a raw JSON object. Do not include markdown formatting or conversational text.
Use this exact structure: 
{{
  ""FlightNumber"": ""Unknown"",
  ""Date"": ""Unknown"",
  ""DepartureTerminal"": ""Unknown"",
  ""Gate"": ""Unknown"",
  ""DepartureTime"": ""Unknown""
}}<|end|>
<|user|>
{rawText}<|end|>
<|assistant|>";

            string jsonResponse = "";
            await foreach (var text in executor.InferAsync(prompt, inferenceParams))
            {
                jsonResponse += text;
            }

            return jsonResponse.Trim();
        }
    }
}

