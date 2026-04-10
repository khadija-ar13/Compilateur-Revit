using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using DesignAutomationFramework;

namespace RfaToRvtPlugin
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ConvertRfaApp : IExternalDBApplication
    {
        public ExternalDBApplicationResult OnStartup(ControlledApplication application)
        {
            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            DesignAutomationBridge.DesignAutomationReadyEvent -= HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            Console.WriteLine("🚀 [DEBUG] Démarrage du placement universel...");
            e.Succeeded = false; 
            
            try
            {
                DesignAutomationData data = e.DesignAutomationData;
                Application app = data.RevitApp;
                Document familyDoc = data.RevitDoc;

                // Création du projet RVT
                Document projectDoc = app.NewProjectDocument(UnitSystem.Metric);

                // 1. Charger la famille (Hors transaction)
                Family loadedFamily = familyDoc.LoadFamily(projectDoc, new CustomFamilyLoadOptions());
                
                if (loadedFamily != null) {
                    using (Transaction t = new Transaction(projectDoc, "Placement Automatique"))
                    {
                        t.Start();
                        
                        FamilySymbol symbol = projectDoc.GetElement(loadedFamily.GetFamilySymbolIds().First()) as FamilySymbol;
                        if (!symbol.IsActive) symbol.Activate();

                        Level level = new FilteredElementCollector(projectDoc).OfClass(typeof(Level)).FirstElement() as Level;
                        XYZ origin = XYZ.Zero;

                        // --- LOGIQUE UNIVERSELLE ---
                        if (loadedFamily.FamilyPlacementType == FamilyPlacementType.OneLevelBased)
                        {
                            // Cas 1 : Objets simples (ex: Paroi de douche, Table)
                            projectDoc.Create.NewFamilyInstance(origin, symbol, level, StructuralType.NonStructural);
                            Console.WriteLine("✅ Objet posé sur le sol.");
                        }
                        else if (loadedFamily.FamilyPlacementType == FamilyPlacementType.HostBasedCurve || 
                                 loadedFamily.FamilyPlacementType == FamilyPlacementType.HostBasedLine ||
                                 loadedFamily.FamilyPlacementType == FamilyPlacementType.HostBasedPoint)
                        {
                            // Cas 2 : Objets muraux (ex: Miroir, Porte)
                            Line wallLine = Line.CreateBound(new XYZ(-5, 0, 0), new XYZ(5, 0, 0));
                            Wall wall = Wall.Create(projectDoc, wallLine, level.Id, false);
                            // On place le miroir sur le mur à 1m20 de hauteur
                            projectDoc.Create.NewFamilyInstance(new XYZ(0, 0, 4.0), symbol, wall, level, StructuralType.NonStructural);
                            Console.WriteLine("✅ Mur créé et objet accroché.");
                        }
                        else
                        {
                            // Cas 3 : Tout autre type (Placement libre)
                            projectDoc.Create.NewFamilyInstance(origin, symbol, StructuralType.NonStructural);
                            Console.WriteLine("✅ Objet placé par défaut.");
                        }
                        
                        t.Commit();
                    }
                }

                projectDoc.SaveAs("result.rvt", new SaveAsOptions { OverwriteExistingFile = true });
                projectDoc.Close(false);
                e.Succeeded = true; 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur : {ex.Message}");
            }
        }
    }

    public class CustomFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues) { overwriteParameterValues = true; return true; }
        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues) { source = FamilySource.Family; overwriteParameterValues = true; return true; }
    }
}
