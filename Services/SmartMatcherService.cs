using AeroSyncAPI.Models;
using Microsoft.ML;
using Microsoft.ML.Transforms.Text;

namespace AeroSyncAPI.Services
{
    public class SmartMatcherService
    {
        private readonly MLContext _mlContext;
        private readonly ITransformer _gloveTransformer;

        public SmartMatcherService()
        {
            _mlContext = new MLContext();
            string modelDir = Path.Combine(AppContext.BaseDirectory, "AI");
            string localModelPath = Path.Combine(modelDir, "glove.6B.50d.txt");

            // 🚀 Trigger our custom progress-bar downloader if the file isn't cached yet
            if (!File.Exists(localModelPath))
            {
                localModelPath = Path.Combine(Directory.GetCurrentDirectory(), "AI", "glove.6B.50d.txt");
                if (!File.Exists(localModelPath))
                {
                    throw new FileNotFoundException($"GloVe data asset missing! Please ensure 'glove.6B.50d.txt' is placed inside your Models folder. Expected path: {localModelPath}");
                }
            }

            var emptyData = new List<PassengerContext>();
            var emptyDataView = _mlContext.Data.LoadFromEnumerable(emptyData);

            // 2. Feed the local file directly into standard ApplyWordEmbedding
            var pipeline = _mlContext.Transforms.Text.TokenizeIntoWords("Tokens", nameof(PassengerContext.InterestsText))
                .Append(_mlContext.Transforms.Text.ApplyWordEmbedding(
                    outputColumnName: "Features",
                    customModelFile: localModelPath,
                    inputColumnName: "Tokens"));

            // 3. Load the model vectors into RAM
            _gloveTransformer = pipeline.Fit(emptyDataView);

        }
        private float[] GetAverageSentenceVector(float[] rawFeatures, int dimensions = 50)
        {
            float[] avgVector = new float[dimensions];
            if (rawFeatures.Length == 0) return avgVector;

            int wordCount = rawFeatures.Length / dimensions;

            // Add all the words together
            for (int i = 0; i < rawFeatures.Length; i++)
            {
                int dimIndex = i % dimensions;
                avgVector[dimIndex] += rawFeatures[i];
            }

            // Divide by the number of words to get the average
            for (int i = 0; i < dimensions; i++)
            {
                avgVector[i] /= wordCount;
            }

            return avgVector;
        }

        public List<MatchScore> FindBestLayoverBuddies(PassengerContext currentUser, List<PassengerContext> allUsers)
        {
            var matchResults = new List<MatchScore>();

            // 1. HARD FILTER: Only look at people in the same terminal with enough time
            var eligibleUsers = allUsers.Where(p =>
             p.EmailHash != currentUser.EmailHash && // 🚀 Check Hash
             p.Terminal == currentUser.Terminal &&
             p.MinutesAvailable >= 45 &&
             Math.Abs(p.MinutesAvailable - currentUser.MinutesAvailable) <= 60
         ).ToList();

            if (!eligibleUsers.Any()) return matchResults;

            // 2. PREPARE ML DATA
            var data = new List<PassengerContext> { currentUser };
            data.AddRange(eligibleUsers);
            
            var dataView = _mlContext.Data.LoadFromEnumerable(data);

            // 4. 🚀 INSTANT TRANSFORM: Use the pre-loaded GloVe model in memory! No .Fit() needed here.
            var transformedData = _gloveTransformer.Transform(dataView);

            // 4. EXTRACT VECTORS
            var vectors = _mlContext.Data.CreateEnumerable<TransformedData>(transformedData, reuseRowObject: false).ToList();

            var rawCurrentUserFeatures = vectors.First(v => v.EmailHash == currentUser.EmailHash).Features;
            var currentUserVector = GetAverageSentenceVector(rawCurrentUserFeatures); // 🚀 Get the 50d Master Vector

            // 5. SCORE MATCHES
            foreach (var user in vectors.Where(v => v.EmailHash != currentUser.EmailHash))
            {
                var otherUserVector = GetAverageSentenceVector(user.Features); // 🚀 Get their 50d Master Vector

                // Now we are comparing exactly 50 dimensions to 50 dimensions!
                float score = CalculateCosineSimilarity(currentUserVector, otherUserVector);

                if (score > 0.3f)
                {
                    var originalUser = eligibleUsers.First(u => u.EmailHash == user.EmailHash);
                    matchResults.Add(new MatchScore
                    {
                        MatchedEmailHash = originalUser.EmailHash,
                        MatchedUserName = originalUser.UserName,
                        MatchedUserInterests = originalUser.InterestsText,
                        SimilarityScore = score
                    });
                }
            }
            return matchResults.OrderByDescending(m => m.SimilarityScore).ToList();
        }

        private float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
        {
            float dotProduct = 0, magnitudeA = 0, magnitudeB = 0;
            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }
            if (magnitudeA == 0 || magnitudeB == 0) return 0;
            return dotProduct / ((float)Math.Sqrt(magnitudeA) * (float)Math.Sqrt(magnitudeB));
        }

        private class TransformedData : PassengerContext
        {
            public float[] Features { get; set; } = { 0, 0, 0, };
        }
    }
}
