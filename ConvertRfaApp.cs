using System;
using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
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
                
                Console.WriteLine("🚀 [DEBUG] Récupération du document RFA ouvert en mémoire...");
                Document familyDoc = data.RevitDoc;

                if (familyDoc == null)
                {
                    Console.WriteLine("❌ [ERREUR] Le document familyDoc est null ! Le fichier RFA n'a pas pu être lu.");
                    return;
                }
                
                if (!familyDoc.IsFamilyDocument)
                {
                    Console.WriteLine("❌ [ERREUR] Le fichier reçu n'est pas reconnu comme une famille RFA valide.");
                    return;
                }

                Console.WriteLine("🚀 [DEBUG] Création d'un nouveau projet RVT vide...");
                Document projectDoc = app.NewProjectDocument(UnitSystem.Metric);

                if (projectDoc == null)
                {
                     Console.WriteLine("❌ [ERREUR] Impossible de créer le nouveau document RVT.");
                     return;
                }

                Console.WriteLine("🚀 [DEBUG] Début de la transaction pour charger la famille...");
                using (Transaction t = new Transaction(projectDoc, "Load RFA into RVT"))
                {
                    t.Start();
                    Console.WriteLine("🚀 [DEBUG] Injection de la famille...");
                    
                    // CORRECTION ICI : La syntaxe correcte pour charger de mémoire à mémoire
                    Family loadedFamily = familyDoc.LoadFamily(projectDoc, new CustomFamilyLoadOptions());
                    
                    if (loadedFamily != null) {
                        Console.WriteLine($"🚀 [DEBUG] Famille chargée avec succès (Nom: {loadedFamily.Name})");
                    } else {
                        Console.WriteLine("⚠️ [ATTENTION] La famille n'a pas pu être chargée.");
                    }
                    
                    t.Commit();
                }

                Console.WriteLine("🚀 [DEBUG] Configuration de la sauvegarde...");
                string outputRvtPath = "result.rvt"; 
                SaveAsOptions saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                
                Console.WriteLine($"🚀 [DEBUG] Sauvegarde du fichier : {outputRvtPath}");
                projectDoc.SaveAs(outputRvtPath, saveOptions);
                
                Console.WriteLine("🚀 [DEBUG] Fermeture du projet...");
                projectDoc.Close(false);

                Console.WriteLine("✅ [SUCCES] Conversion terminée sans erreur.");
                e.Succeeded = true; 
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ [CRASH INTERNE C#] Une exception a été levée :");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }
    }

    public class CustomFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
