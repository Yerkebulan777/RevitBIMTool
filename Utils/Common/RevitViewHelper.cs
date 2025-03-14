using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Serilog;
using Color = Autodesk.Revit.DB.Color;
using Level = Autodesk.Revit.DB.Level;
using View = Autodesk.Revit.DB.View;


namespace RevitBIMTool.Utils;
internal sealed class RevitViewHelper
{

    #region 3dView

    public static View3D Create3DView(Document doc, string viewName)
    {
        View3D view3d = null;

        ViewFamilyType viewFamly = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .First(q => q.ViewFamily == ViewFamily.ThreeDimensional);

        using (Transaction trx = new(doc, "Create3DView"))
        {
            TransactionStatus status = trx.Start();

            if (status == TransactionStatus.Started)
            {
                try
                {
                    view3d = View3D.CreateIsometric(doc, viewFamly.Id);
                    view3d.Name = viewName;
                    status = trx.Commit();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);

                    if (!trx.HasEnded())
                    {
                        status = trx.RollBack();
                    }
                }
                finally
                {
                    viewFamly.Dispose();
                }
            }
        }

        return view3d;
    }


    public static View3D Get3dView(Document doc, string viewName = "3DView")
    {
        foreach (View3D view3d in new FilteredElementCollector(doc).OfClass(typeof(View3D)).OfType<View3D>())
        {
            if (!view3d.IsTemplate && view3d.Name.Equals(viewName))
            {
                return view3d;
            }
        }
        return Create3DView(doc, viewName);
    }

    #endregion


    #region PlanView

    public static ViewPlan GetPlanByLevel(UIDocument uidoc, Level level, string prefix = "Preview")
    {
        if (level == null || uidoc == null || !uidoc.IsValidObject)
        {
            return null;
        }
        Document doc = uidoc.Document;
        string viewName = prefix + level.Name.Trim();
        foreach (ViewPlan plan in new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).OfType<ViewPlan>())
        {
            if (!plan.IsTemplate && level.Id.Equals(plan.GenLevel.Id))
            {
                return plan;
            }
        }
        return CreatePlanView(doc, level, viewName);
    }


    public static ViewPlan CreatePlanView(Document doc, Level level, string name)
    {
        ViewPlan floorPlan = null;
        TransactionHelpers.CreateTransaction(doc, "CreateFloorPlan", () =>
        {
            ViewFamilyType vft = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
            .First(x => ViewFamily.FloorPlan == x.ViewFamily);
            floorPlan = ViewPlan.Create(doc, vft.Id, level.Id);
            floorPlan.DisplayStyle = DisplayStyle.ShadingWithEdges;
            floorPlan.Discipline = ViewDiscipline.Coordination;
            floorPlan.DetailLevel = ViewDetailLevel.Fine;
            floorPlan.Name = name;
        });

        return floorPlan;
    }

    #endregion


    #region Settings

    public static void SetViewSettings(Document doc, View view, ViewDiscipline discipline, DisplayStyle style, ViewDetailLevel detail)
    {
        using (Transaction trans = new(doc))
        {
            try
            {
                TransactionStatus status = trans.Start("SetViewSettings");
                if (status == TransactionStatus.Started && view is View3D view3D)
                {

                    view3D.ViewTemplateId = ElementId.InvalidElementId;
                    view3D.IsSectionBoxActive = false;
                    view3D.Discipline = discipline;
                    view3D.DisplayStyle = style;
                    view3D.DetailLevel = detail;

                    trans.Commit();
                }
            }
            finally
            {
                Log.Debug("Set View settings");
            }

        };
    }

    #endregion


    #region ZoomInView
    public static void ZoomElementInView(UIDocument uidoc, View view, BoundingBoxXYZ box)
    {
        UIView uiview = uidoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId.Equals(view.Id));
        if (box != null && box.Enabled)
        {
            uiview?.ZoomAndCenterRectangle(box.Min, box.Max);
        }
    }
    #endregion


    #region BoundingBox
    public static BoundingBoxXYZ CreateBoundingBox(XYZ centroid, double offset = 7)
    {
        BoundingBoxXYZ bbox = new();
        XYZ vector = new(offset, offset, offset);
        bbox.Min = centroid - vector;
        bbox.Max = centroid + vector;
        bbox.Enabled = true;
        return bbox;
    }


    public static BoundingBoxXYZ CreateBoundingBox(View view, Element element, XYZ centroid, double offset = 7)
    {
        BoundingBoxXYZ bbox = element.get_BoundingBox(view);
        if (centroid != null && bbox != null && bbox.Enabled)
        {
            bbox.Min = new XYZ(centroid.X - offset, centroid.Y - offset, bbox.Min.Z);
            bbox.Max = new XYZ(centroid.X + offset, centroid.Y + offset, bbox.Max.Z);
        }
        else
        {
            bbox = CreateBoundingBox(centroid, offset);
            bbox.Min = new XYZ(bbox.Min.X, bbox.Min.Y, view.Origin.Z);
            bbox.Max = new XYZ(bbox.Max.X, bbox.Max.Y, view.Origin.Z);
        }
        return bbox;
    }

    #endregion


    #region Categories

    public static void SetCategoriesToVisible(Document doc, View view, IList<BuiltInCategory> toHide = null)
    {
        using Transaction trx = new(doc, "CategoriesVisible");

        try
        {
            if (TransactionStatus.Started == trx.Start())
            {
                foreach (ElementId catId in ParameterFilterUtilities.GetAllFilterableCategories())
                {
                    Category cat = Category.GetCategory(doc, (BuiltInCategory)catId.IntegerValue);

                    if (cat is Category category && view.CanCategoryBeHidden(category.Id))
                    {
                        if (toHide != null && toHide.Contains((BuiltInCategory)catId.IntegerValue))
                        {
                            view.SetCategoryHidden(catId, true);
                        }
                        else if (cat.SubCategories.Size > 0)
                        {
                            view.SetCategoryHidden(catId, false);
                        }
                    }
                }

                trx.Commit();
            }
        }
        finally
        {
            Log.Debug("Set categories to visible");

            if (!trx.HasEnded())
            {
                trx.RollBack();
            }
        }

    }


    public static void SetCategoryTransparency(Document doc, View3D view, Category category, int transparency = 15, bool halftone = false)
    {
        ElementId catId = category.Id;
        OverrideGraphicSettings graphics;
        if (view.IsCategoryOverridable(catId))
        {
            graphics = new OverrideGraphicSettings();
            graphics = graphics.SetHalftone(halftone);
            graphics = graphics.SetSurfaceTransparency(transparency);
            TransactionHelpers.CreateTransaction(doc, "Override", () =>
            {
                view.SetCategoryOverrides(catId, graphics);
            });
        }
    }

    #endregion


    #region CustomColor
    public static ElementId GetSolidFillPatternId(Document doc)
    {
        ElementId solidFillPatternId = null;
        FilteredElementCollector collector = new FilteredElementCollector(doc)
        .WherePasses(new ElementClassFilter(typeof(FillPatternElement)));
        foreach (FillPatternElement fp in collector.OfType<FillPatternElement>())
        {
            FillPattern pattern = fp.GetFillPattern();
            if (pattern != null && pattern.IsSolidFill)
            {
                solidFillPatternId = fp.Id;
                break;
            }
        }
        return solidFillPatternId;
    }

    #endregion


    #region Colorize

    //public static void ColorizeElements(Document doc, Transform trf, ViewElement view, ICollection<ElementId> ids, string styleName)
    //{
    //    SpatialFieldManager sfm = SpatialFieldManager.GetSpatialFieldManager(view) ?? SpatialFieldManager.CreateSpatialFieldManager(view, 1);
    //    AnalysisResultSchema resultSchema = new AnalysisResultSchema(styleName, "Changes analisis");
    //    foreach (ElementId changedId in ids)
    //    {
    //        Element element = doc.GetElement(changedId);
    //        if (element != null)
    //        {
    //            List<Element> elements = new List<Element>
    //            {
    //                element
    //            };
    //            foreach (Face face in SolidHelper.GetFaces(elements, trf))
    //            {
    //                PaintFace(face, sfm, resultSchema, trf);
    //            }
    //        }
    //    }
    //}


    //static void PaintFace(Face face, SpatialFieldManager sfm, AnalysisResultSchema resultSchema, Transform transform)
    //{
    //    IList<UV> uvPts = new List<UV>();
    //    List<double> doubleList = new List<double>();
    //    IList<ValueAtPoint> valList = new List<ValueAtPoint>();

    //    int idx = sfm.AddSpatialFieldPrimitive(face, transform);
    //    BoundingBoxUV bb = face.GetBoundingBox();
    //    UV min = bb.Min;
    //    UV max = bb.Max;
    //    uvPts.Add(new UV(min.U, min.V));
    //    uvPts.Add(new UV(max.U, max.V));

    //    doubleList.Add(0);
    //    valList.Add(new ValueAtPoint(doubleList));
    //    doubleList.Clear();
    //    doubleList.Add(10);
    //    valList.Add(new ValueAtPoint(doubleList));

    //    FieldValues vals = new FieldValues(valList);
    //    int schemaIndex = sfm.RegisterResult(resultSchema);
    //    FieldDomainPointsByUV pnts = new FieldDomainPointsByUV(uvPts);
    //    sfm.UpdateSpatialFieldPrimitive(idx, pnts, vals, schemaIndex);
    //}


    //public static void CreateAVFDisplayStyle(Document doc, ViewElement view, string styleName)
    //{
    //    FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(AnalysisDisplayStyle));

    //    if (!collector.Any(a => a.PrinterName == styleName))
    //    {
    //        AnalysisDisplayMarkersAndTextSettings markerSettings = new AnalysisDisplayMarkersAndTextSettings();

    //        AnalysisDisplayColorSettings colorSettings = new AnalysisDisplayColorSettings();

    //        AnalysisDisplayLegendSettings legendSettings = new AnalysisDisplayLegendSettings();

    //        legendSettings.ShowLegend = true;

    //        AnalysisDisplayStyle displayStyle = AnalysisDisplayStyle.CreateAnalysisDisplayStyle(doc, styleName, markerSettings, colorSettings, legendSettings);

    //        view.AnalysisDisplayStyleId = displayStyle.Id;
    //    }
    //}

    #endregion


    #region ViewFilter

    public static void CreateViewFilter(Document doc, View view, Element elem, ElementFilter filter)
    {
        string filterName = "Filter" + elem.Name;
        OverrideGraphicSettings ogSettings = new();
        IList<ElementId> categories = CheckFilterableCategoryByElement(elem);
        ParameterFilterElement prmFilter = ParameterFilterElement.Create(doc, filterName, categories, filter);
        ogSettings = ogSettings.SetProjectionLineColor(new Color(255, 0, 0));
        view.SetFilterOverrides(prmFilter.Id, ogSettings);
    }


    private static IList<ElementId> CheckFilterableCategoryByElement(Element elem)
    {
        ICollection<ElementId> catIds = ParameterFilterUtilities.GetAllFilterableCategories();
        IList<ElementId> categories = [];
        foreach (ElementId catId in catIds)
        {
            if (elem.Category.Id == catId)
            {
                categories.Add(catId);
                break;
            }
        }
        return categories;
    }

    #endregion


    #region ViewSheet

    public static void OpenView(UIDocument uidoc, View view)
    {
        ActivateView(uidoc, view);

        IList<UIView> allviews = uidoc.GetOpenUIViews();

        if (view.IsValidObject && allviews.Count > 1)
        {
            foreach (UIView uv in allviews)
            {
                try
                {
                    if (view.Id == uv.ViewId)
                    {
                        uv.ZoomSheetSize();
                    }
                    else
                    {
                        uv.Close();
                        uv.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }
        }
    }


    private static void ActivateView(UIDocument uidoc, View view)
    {
        lock (uidoc)
        {
            try
            {
                uidoc.ActiveView = view;
                uidoc.RefreshActiveView();
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
            finally
            {
                Log.Debug($"Activated view: {view.Name}");
            }
        }
    }

    #endregion


    public static void CropAroundRoom(Room room, View view)
    {
        if (view != null)
        {
            IList<IList<BoundarySegment>> segments = room.GetBoundarySegments(new SpatialElementBoundaryOptions());

            if (segments != null)  //the room may not be bound
            {
                foreach (IList<BoundarySegment> segmentList in segments)
                {
                    CurveLoop loop = new();

                    foreach (BoundarySegment boundarySegment in segmentList)
                    {
                        loop.Append(boundarySegment.GetCurve());
                    }

                    ViewCropRegionShapeManager vcrShapeMgr = view.GetCropRegionShapeManager();

                    vcrShapeMgr.SetCropShape(loop);

                    break;  // if more than one set of boundary segments for room, crop around the first one
                }
            }
        }
    }


    public static void HideElementsInView(Document doc, IList<Element> elements, View activeView)
    {
        using Transaction trx = new(doc, "HideElements");

        try
        {
            List<ElementId> hideIds = [];

            if (TransactionStatus.Started == trx.Start())
            {
                foreach (Element instance in elements)
                {
                    if (instance.CanBeHidden(activeView))
                    {
                        if (!instance.IsHidden(activeView))
                        {
                            hideIds.Add(instance.Id);
                        }
                    }
                }

                if (hideIds.Count > 0)
                {
                    activeView.HideElements(hideIds);
                    trx.Commit();
                }
            }
        }
        finally
        {
            Log.Debug("Hided elements");

            if (!trx.HasEnded())
            {
                trx.RollBack();
            }
        }

    }

}