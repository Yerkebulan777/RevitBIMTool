using Autodesk.Revit.DB;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;


namespace RevitBIMTool.Core
{
    /// <summary>
    /// Core processor for lintel marking algorithm
    /// </summary>
    public class LintelProcessor
    {
        private readonly MarkingConfig _config;

        /// <summary>
        /// Initializes a new instance of LintelProcessor
        /// </summary>
        /// <param name="config">Configuration for marking algorithm</param>
        public LintelProcessor(MarkingConfig config = null)
        {
            _config = config ?? new MarkingConfig();
        }

        /// <summary>
        /// Processes collection of lintels and returns marking data
        /// </summary>
        /// <param name="lintels">Collection of lintel family instances</param>
        /// <returns>Dictionary mapping each lintel to its marking data</returns>
        public Dictionary<FamilyInstance, LintelData> Process(ICollection<FamilyInstance> lintels)
        {
            if (lintels != null && lintels.Count != 0)
            {
                // Step 1: Extract data from family instances
                Dictionary<FamilyInstance, LintelData> data = ExtractData(lintels);

                // Step 2: Group lintels by rounded dimensions
                Dictionary<string, List<FamilyInstance>> groups = GroupLintels(data);

                // Step 3: Merge small groups with similar larger groups
                MergeGroups(groups, data);

                // Step 4: Assign marks based on final grouping
                AssignMarks(groups, data);

                return data;
            }

            return [];
        }

        /// <summary>
        /// Extracts dimension data from family instances
        /// </summary>
        private Dictionary<FamilyInstance, LintelData> ExtractData(ICollection<FamilyInstance> lintels)
        {
            Dictionary<FamilyInstance, LintelData> result = [];

            foreach (FamilyInstance lintel in lintels)
            {
                // Get parameter values
                double thickness = LintelUtils.GetParameterValue(lintel, _config.ThicknessParam);
                double height = LintelUtils.GetParameterValue(lintel, _config.HeightParam);
                double width = LintelUtils.GetParameterValue(lintel, _config.WidthParam);

                // Round values
                int thicknessRound = LintelUtils.Round50(thickness);
                int heightRound = LintelUtils.Round50(height);
                int widthRound = LintelUtils.Round50(width);

                // Create lintel data
                LintelData lintelData = new()
                {
                    Width = width,
                    Height = height,
                    Thickness = thickness,
                    ThicknessRound = thicknessRound,
                    WidthRound = widthRound,
                    HeightRound = heightRound,
                    Group = $"{thicknessRound}_{widthRound}_{heightRound}"
                };

                result[lintel] = lintelData;
            }

            return result;
        }

        /// <summary>
        /// Groups lintels by rounded dimensions
        /// </summary>
        private Dictionary<string, List<FamilyInstance>> GroupLintels(Dictionary<FamilyInstance, LintelData> data)
        {
            Dictionary<string, List<FamilyInstance>> groups = [];

            foreach (KeyValuePair<FamilyInstance, LintelData> pair in data)
            {
                string group = pair.Value.Group;

                if (!groups.ContainsKey(group))
                {
                    groups[group] = [];
                }

                groups[group].Add(pair.Key);
            }

            return groups;
        }

        /// <summary>
        /// Merges small groups with similar larger groups
        /// </summary>
        private void MergeGroups(Dictionary<string, List<FamilyInstance>> groups, Dictionary<FamilyInstance, LintelData> data)
        {
            // Identify small groups
            List<string> smallGroups = groups
                .Where(g => g.Value.Count < _config.MinGroupSize)
                .Select(g => g.Key)
                .ToList();

            // Identify large groups
            List<string> largeGroups = groups
                .Where(g => g.Value.Count >= _config.MinGroupSize)
                .Select(g => g.Key)
                .ToList();

            // Process small groups
            foreach (string smallGroup in smallGroups)
            {
                int[] smallValues = smallGroup.Split('_').Select(int.Parse).ToArray();

                string bestMatch = null;
                int minDifference = int.MaxValue;

                // Find closest large group
                foreach (string largeGroup in largeGroups)
                {
                    int[] largeValues = largeGroup.Split('_').Select(int.Parse).ToArray();

                    // Calculate differences
                    int diffThickness = Math.Abs(smallValues[0] - largeValues[0]);
                    int diffWidth = Math.Abs(smallValues[1] - largeValues[1]);
                    int diffHeight = Math.Abs(smallValues[2] - largeValues[2]);

                    // Check if within threshold
                    if (diffThickness <= _config.Threshold &&
                        diffWidth <= _config.Threshold &&
                        diffHeight <= _config.Threshold)
                    {
                        int totalDiff = diffThickness + diffWidth + diffHeight;

                        if (totalDiff < minDifference)
                        {
                            minDifference = totalDiff;
                            bestMatch = largeGroup;
                        }
                    }
                }

                // Merge with best match if found
                if (bestMatch != null)
                {
                    // Update group assignment in lintel data
                    foreach (FamilyInstance lintel in groups[smallGroup])
                    {
                        data[lintel].Group = bestMatch;
                    }

                    // Add elements to large group
                    groups[bestMatch].AddRange(groups[smallGroup]);

                    // Remove small group (will be skipped in mark assignment)
                    _ = groups.Remove(smallGroup);
                }
            }
        }

        /// <summary>
        /// Assigns marks based on final grouping
        /// </summary>
        private void AssignMarks(Dictionary<string, List<FamilyInstance>> groups, Dictionary<FamilyInstance, LintelData> data)
        {
            // Sort groups for consistent mark assignment
            List<string> sortedGroups = groups.Keys
                .OrderBy(g => int.Parse(g.Split('_')[0]))  // First by thickness
                .ThenBy(g => int.Parse(g.Split('_')[1]))   // Then by width
                .ThenBy(g => int.Parse(g.Split('_')[2]))   // Then by height
                .ToList();

            // Assign mark to each group

            for (int idx = 0; idx < sortedGroups.Count; idx++)
            {
                string group = sortedGroups[idx];
                string mark = $"{_config.MarkPrefix}{idx + 1}";

                // Update mark in lintel data

                foreach (FamilyInstance lintel in groups[group])
                {
                    data[lintel].Mark = mark;
                }
            }
        }
    }
}
