using System;
using System.Linq;
using System.Collections.Generic; // <--- LA LIGNE MAGIQUE QUI MANQUAIT
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
            Console.WriteLine("🚀 [DEBUG] Début de l'exécution du plugin...");
            e.Succeeded = false; 
            
            try
            {
                DesignAutomationData data = e.DesignAutomationData;
                Application app = data.RevitApp;
                Document familyDoc = data.RevitDoc;

                if (familyDoc == null || !familyDoc.IsFamilyDocument)
                {
                    Console.WriteLine("❌ [ERREUR] Le fichier reçu n'est pas une famille valide.");
                    return;
                }

                Console.WriteLine("🚀 [DEBUG] Création d'un nouveau projet RVT vide...");
                Document projectDoc = app.NewProjectDocument(UnitSystem.Metric);

                Console.WriteLine("🚀 [DEBUG] Injection et placement de la famille...");
                using (Transaction t = new Transaction(projectDoc, "Load and Place Family"))
                {
                    t.Start();
                    
                    Family loadedFamily = familyDoc.LoadFamily(projectDoc, new CustomFamilyLoadOptions());
                    
                    if (loadedFamily != null) {
                        Console.WriteLine($"🚀 [DEBUG] Famille chargée (Nom: {loadedFamily.Name}). Recherche d'un type...");
                        
                        // Obtenir le premier type (symbole) de la famille
                        FamilySymbol symbolToPlace = null;
                        ISet<ElementId> symbolIds = loadedFamily.GetFamilySymbolIds();
                        
                        if (symbolIds.Count > 0)
                        {
                            symbolToPlace = projectDoc.GetElement(symbolIds.First()) as FamilySymbol;
                        }

                        if (symbolToPlace != null)
                        {
                            // Activer le symbole s'il ne l'est pas
                            if (!symbolToPlace.IsActive)
                            {
                                symbolToPlace.Activate();
                                projectDoc.Regenerate();
                            }

                            // Placer l'objet au centre (0,0,0)
                            XYZ origin = new XYZ(0, 0, 0);
                            
                            // Déterminer le type de placement (basé sur le niveau)
                            Level defaultLevel = new FilteredElementCollector(projectDoc)
                                .OfClass(typeof(Level))
                                .FirstElement() as Level;

                            if (defaultLevel != null)
                            {
                                projectDoc.Create.NewFamilyInstance(origin, symbolToPlace, defaultLevel, StructuralType.NonStructural);
                                Console.WriteLine("✅ [DEBUG] Instance placée au centre du projet !");
                            }
                            else
                            {
                                projectDoc.Create.NewFamilyInstance(origin, symbolToPlace, StructuralType.NonStructural);
                                Console.WriteLine("✅ [DEBUG] Instance placée (sans niveau par défaut) !");
                            }
                        }
                        else
                        {
                            Console.WriteLine("⚠️ [ATTENTION] Aucun type (symbole) trouvé dans cette famille.");
                        }
                    } else {
                        Console.WriteLine("⚠️ [ATTENTION] La famille n'a pas pu être chargée.");
                    }
                    
                    t.Commit();
                }

                string outputRvtPath = "result.rvt"; 
                SaveAsOptions saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                projectDoc.SaveAs(outputRvtPath, saveOptions);
                projectDoc.Close(false);

                Console.WriteLine("✅ [SUCCES] Conversion terminée.");
                e.Succeeded = true; 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [CRASH INTERNE C#] {ex.Message}");
            }
        }
    }

    public class CustomFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues) { overwriteParameterValues = true; return true; }
        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues) { source = FamilySource.Family; overwriteParameterValues = true; return true; }
    }
}
