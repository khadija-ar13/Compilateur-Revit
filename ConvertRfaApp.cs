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
            e.Succeeded = false; // On part du principe que ça échoue, on mettra True à la fin
            try
            {
                DesignAutomationData data = e.DesignAutomationData;
                Application app = data.RevitApp;
                
                // Le fichier input.rfa est DÉJÀ ouvert par le Cloud, on le récupère directement de la mémoire !
                Document familyDoc = data.RevitDoc;

                if (familyDoc == null || !familyDoc.IsFamilyDocument)
                {
                    Console.WriteLine("❌ ERREUR: Le fichier reçu n'est pas un RFA valide.");
                    return;
                }

                Console.WriteLine("⚙️ 1. Création du projet RVT...");
                Document projectDoc = app.NewProjectDocument(UnitSystem.Metric);

                Console.WriteLine("⚙️ 2. Chargement de la famille (De mémoire à mémoire)...");
                using (Transaction t = new Transaction(projectDoc, "Load RFA into RVT"))
                {
                    t.Start();
                    // On injecte la famille dans le projet avec une option pour ignorer les pop-ups
                    familyDoc.LoadFamily(projectDoc, new CustomFamilyLoadOptions());
                    t.Commit();
                }

                Console.WriteLine("⚙️ 3. Sauvegarde du fichier result.rvt...");
                string outputRvtPath = "result.rvt";
                SaveAsOptions saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
                projectDoc.SaveAs(outputRvtPath, saveOptions);
                projectDoc.Close(false);

                Console.WriteLine("✅ CONVERSION RÉUSSIE !");
                e.Succeeded = true; // On valide l'opération pour le serveur Autodesk
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CRASH INTERNE: {ex.Message}");
            }
        }
    }

    // Cette classe est obligatoire pour dire à Revit d'écraser les fichiers sans afficher de Pop-up d'alerte
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
