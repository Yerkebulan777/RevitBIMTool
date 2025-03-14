using Autodesk.Revit.DB;


namespace RevitBIMTool.Utils.Performance
{
    internal class FlippedDoorCheck : IPerformanceAdviserRule
    {
        private readonly string m_name;
        private readonly string m_description;
        private readonly FailureDefinitionId m_doorWarningId;
        private readonly FailureDefinition m_doorWarning;
        private List<ElementId> m_FlippedDoors;

        #region Constructor

        public FlippedDoorCheck()
        {
            m_name = "Flipped Door Check";
            m_description = "An API-based rule to search for and return any doors that are face-flipped";
            m_doorWarningId = new FailureDefinitionId(new Guid("25570B8FD4AD42baBD78469ED60FB9A3"));
            m_doorWarning = FailureDefinition.CreateFailureDefinition(m_doorWarningId, FailureSeverity.Warning, "Some doors in this project are face-flipped.");
        }

        #endregion


        #region IPerformanceAdviserRule implementation

        /// <summary>
        /// Does some preliminary work before executing tests on elements.  In this case,
        /// we instantiate a list of FamilyInstances representing all doors that are flipped.
        /// </summary>
        public void InitCheck(Document document)
        {
            if (m_FlippedDoors == null)
            {
                m_FlippedDoors = [];
            }
            else
            {
                m_FlippedDoors.Clear();
            }

            return;
        }

        /// <summary>
        /// This method does most of the work of the IPerformanceAdviserRule implementation.
        /// It is called by Performance.
        /// It examines the element passed to it (which was previously filtered by the filter
        /// returned by GetElementFilter() (see below)).  After checking to make sure that the
        /// element is an instance, it checks the FacingFlipped property of the element.
        /// If it is flipped, it adds the instance to a list to be used later.
        /// </summary>

        public void ExecuteElementCheck(Document document, Element element)
        {
            if (element is FamilyInstance doorCurrent)
            {
                if (doorCurrent.FacingFlipped)
                {
                    m_FlippedDoors.Add(doorCurrent.Id);
                }
            }
        }

        /// <summary>
        /// This method is called by Performance after all elements in document
        /// matching the ElementFilter from GetElementFilter() are checked by ExecuteElementCheck().
        /// This method checks to see if there are any elements (door instances, in this case) in the
        /// m_FlippedDoor instance member.  If there are, it iterates through that list and displays
        /// the instance name and door tag of each item.
        /// </summary>
        public void FinalizeCheck(Document document)
        {
            if (m_FlippedDoors.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No doors were flipped.  Test passed.");
            }
            else
            {
                //Pass the element IDs of the flipped doors to the revit failure reporting APIs.
                FailureMessage fm = new(m_doorWarningId);
                fm.SetFailingElements(m_FlippedDoors);
                Transaction failureReportingTransaction = new(document, "Failure reporting transaction");
                failureReportingTransaction.Start();
                document.PostFailure(fm);
                failureReportingTransaction.Commit();
                m_FlippedDoors.Clear();
            }
        }

        /// <summary>
        /// Gets the description of the rule
        /// </summary>
        public string GetDescription()
        {
            return m_description;
        }

        /// <summary>
        /// This method supplies an element filter to reduce the number of elements that Performance
        /// will pass to GetElementCheck().  In this case, we are filtering for door elements.
        /// </summary>
        public ElementFilter GetElementFilter(Document document)
        {
            return new ElementCategoryFilter(BuiltInCategory.OST_Doors);
        }

        /// <summary>
        /// Gets the name of the rule
        /// </summary>
        public string GetName()
        {
            return m_name;
        }

        /// <summary>
        /// Returns true if this rule will iterate through elements and check them, false otherwise
        /// </summary>
        public bool WillCheckElements()
        {
            return true;
        }

        #endregion


    }
}
