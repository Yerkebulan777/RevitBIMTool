using Autodesk.Revit.DB;
using RevitBIMTool.Core;
using RevitBIMTool.Model;
using RevitBIMTool.Utils;


namespace RevitBIMTool.Services
{
    /// <summary>
    /// Service for marking lintels in Revit document
    /// </summary>
    public class LintelMarkingService
    {
        private readonly Document _doc;
        private readonly MarkingConfig _config;
        private readonly LintelProcessor _processor;

        /// <summary>
        /// Initializes a new instance of LintelMarkingService
        /// </summary>
        /// <param name="document">Revit document</param>
        /// <param name="config">Configuration for marking algorithm</param>
        public LintelMarkingService(Document document, MarkingConfig config = null)
        {
            _doc = document;
            _config = config ?? new MarkingConfig();
            _processor = new LintelProcessor(_config);
        }

        /// <summary>
        /// Finds lintel family instances in the document
        /// </summary>
        /// <param name="categoryId">Category ID (default: StructuralFraming)</param>
        /// <param name="nameFilters">Family name filters</param>
        /// <returns>Collection of lintel family instances</returns>
        public ICollection<FamilyInstance> FindLintels(
            ElementId categoryId = null,
            IEnumerable<string> nameFilters = null)
        {
            // Use structural framing category if not specified
            if (categoryId == null)
            {
                categoryId = new ElementId(BuiltInCategory.OST_StructuralFraming);
            }

            // Default name filters if not specified
            nameFilters ??= new[] { "Перемычка", "перемычка", "Lintel", "lintel" };

            // Create collector
            FilteredElementCollector collector = new(_doc);

            // Filter by class and category
            ICollection<FamilyInstance> instances = collector
                .OfClass(typeof(FamilyInstance))
                .OfCategoryId(categoryId)
                .Cast<FamilyInstance>()
                .ToList();

            // Filter by family name
            return instances
                .Where(fi => nameFilters.Any(filter =>
                    fi.Symbol.FamilyName.Contains(filter)))
                .ToList();
        }

        /// <summary>
        /// Marks lintels with automatic unification
        /// </summary>
        /// <param name="lintels">Collection of lintel family instances</param>
        /// <returns>Number of marked lintels</returns>
        public int MarkLintels(ICollection<FamilyInstance> lintels)
        {
            if (lintels == null || lintels.Count == 0)
            {
                return 0;
            }

            // Process lintels to get marking data
            Dictionary<FamilyInstance, LintelData> data = _processor.Process(lintels);

            // Apply marks in a transaction
            using Transaction t = new(_doc, "Mark Lintels");
            _ = t.Start();

            int marked = 0;

            foreach (KeyValuePair<FamilyInstance, LintelData> pair in data)
            {
                if (pair.Value.Mark != null)
                {
                    if (LintelUtils.SetMark(pair.Key, pair.Value.Mark, _config.MarkParam))
                    {
                        marked++;
                    }
                }
            }

            _ = t.Commit();

            return marked;
        }

        /// <summary>
        /// Finds and marks all lintels in the document
        /// </summary>
        /// <returns>Number of marked lintels</returns>
        public int MarkAllLintels()
        {
            ICollection<FamilyInstance> lintels = FindLintels();
            return MarkLintels(lintels);
        }
    }

}
