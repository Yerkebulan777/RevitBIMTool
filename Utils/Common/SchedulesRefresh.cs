using Autodesk.Revit.DB;


namespace RevitBIMTool.Utils
{
    public static class SchedulesRefresh
    {
        public static List<long> groupIds = [];

        public static void Start(Document doc, View sheet)
        {
            List<ScheduleSheetInstance> instances = new FilteredElementCollector(doc)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>()
                .Where(i => i.OwnerViewId.IntegerValue == sheet.Id.IntegerValue)
                .Where(i => !i.IsTitleblockRevisionSchedule)
                .ToList();

            if (instances.Count == 0) { return; }

            List<ScheduleSheetInstance> pinnedSchedules = [];

            using (Transaction trx1 = new(doc, "SchedulesRefresh1"))
            {
                if (TransactionStatus.Started == trx1.Start())
                {
                    foreach (ScheduleSheetInstance ssi in instances)
                    {
                        if (ssi.Pinned && ssi.GroupId == ElementId.InvalidElementId)
                        {
                            ssi.Pinned = false;
                            pinnedSchedules.Add(ssi);
                        }

                        MoveScheduleOrGroup(doc, ssi, 0.1);
                    }
                    trx1.Commit();
                }
            }

            groupIds.Clear();

            using (Transaction trx2 = new(doc, "SchedulesRefresh2"))
            {
                if (TransactionStatus.Started == trx2.Start())
                {
                    foreach (ScheduleSheetInstance ssi in instances)
                    {
                        MoveScheduleOrGroup(doc, ssi, -0.1);
                    }

                    foreach (ScheduleSheetInstance ssi in pinnedSchedules)
                    {
                        ssi.Pinned = true;
                    }

                    trx2.Commit();
                }
            }
        }


        private static void MoveScheduleOrGroup(Document doc, ScheduleSheetInstance ssi, double distance)
        {
            if (ssi.GroupId == null || ssi.GroupId == ElementId.InvalidElementId)
            {
                ElementTransformUtils.MoveElement(doc, ssi.Id, new XYZ(distance, 0, 0));
            }
            else
            {
                int groupId = ssi.GroupId.IntegerValue;
                Element group = doc.GetElement(ssi.GroupId);
                if (groupIds.Contains(groupId))
                {
                    return;
                }

                if (group.Pinned)
                {
                    group.Pinned = false;
                }

                ElementTransformUtils.MoveElement(doc, ssi.GroupId, new XYZ(distance, 0, 0));

                groupIds.Add(groupId);
            }
        }
    }
}
