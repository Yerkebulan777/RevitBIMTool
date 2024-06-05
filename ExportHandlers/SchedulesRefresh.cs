#region License
/*Данный код опубликован под лицензией Creative Commons Attribution-ShareAlike.
Разрешено использовать, распространять, изменять и брать данный код за основу для производных в коммерческих и
некоммерческих целях, при условии указания авторства и если производные лицензируются на тех же условиях.
Код поставляется "как есть". Автор не несет ответственности за возможные последствия использования.
Зуев Александр, 2020, все права защищены.
This code is listed under the Creative Commons Attribution-ShareAlike license.
You may use, redistribute, remix, tweak, and build upon this work non-commercially and commercially,
as long as you credit the author by linking back and license your new creations under the same terms.
This code is provided 'as is'. Author disclaims any implied warranty.
Zuev Aleksandr, 2020, all rigths reserved.*/
#endregion
#region usings
using Autodesk.Revit.DB;
#endregion

namespace RevitBIMTool.ExportHandlers
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
                    _ = trx1.Commit();
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

                    _ = trx2.Commit();
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
