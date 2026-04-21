using System.IO;
using System.Windows.Controls;
using System.Windows.Forms;
using AGR_PropManager;
using Agrovent.Infrastructure.Enums;
using Agrovent.Services;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Xarial.XCad;
using Xarial.XCad.Documents;
using Xarial.XCad.Documents.Extensions;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.Documents;
using Xarial.XCad.SolidWorks.Documents.Services;

namespace Agrovent.Infrastructure.Handlers
{
    public class AGR_DocumentHandler : SwDocumentHandler
    {
        private readonly ISwAddInEx m_AddIn;
        private ISwDocument m_Doc;
        private IAGR_ViewModelCacheService _viewModelCache;
        private IAGR_ComponentViewModelFactory _viewModelFactory;


        public AGR_DocumentHandler(ISwAddInEx addIn, IAGR_ViewModelCacheService _ViewModelCacheService, IAGR_ComponentViewModelFactory viewModelFactory)
        {
            m_AddIn = addIn;
            _viewModelCache = _ViewModelCacheService;
            _viewModelFactory = viewModelFactory;
        }

        protected override void OnInit(ISwApplication app, ISwDocument doc)
        {
            m_Doc = doc;

            m_AddIn.Application.Documents.DocumentActivated += Application_DocumentActivated;
            
            if (doc is ISwPart part)
            {
                var swPart = part.Part as PartDoc;
                swPart.FileSaveAsNotify2 += OnFileSaveAsNotify2;
                swPart.FileSaveNotify += OnFileSaveNotify;
                swPart.FeatureManagerTreeRebuildNotify += SwPart_FeatureManagerTreeRebuildNotify;
                swPart.FeatureEditPreNotify += SwPart_FeatureEditPreNotify;
                part.Features.FeatureCreated += Features_FeatureCreated;
            }
            if (doc is ISwAssembly assembly)
            {
                var swAssembly = assembly.Assembly as AssemblyDoc;
                swAssembly.FileSaveAsNotify2 += OnFileSaveAsNotify2;
            }
        }

        private void Features_FeatureCreated(IXDocument doc, Xarial.XCad.Features.IXFeature feature)
        {
            if (true)
            {

            }
        }

        private int SwPart_FeatureEditPreNotify(object EditFeature)
        {
            if (true)
            {

            }
            return 0;
        }

        private int SwPart_FeatureManagerTreeRebuildNotify()
        {
            if (true)
            {
                var doc = m_AddIn.Application.Documents.Active as ISwDocument3D;
                var vm = _viewModelCache.GetOrCreate(doc, d => _viewModelFactory.CreateComponent(d));
                
            }
            return 0;
        }

        private int OnFileSaveNotify(string FileName)
        {
            if (FileName.Contains(AGR_Options.StorageRootFolderPath))
            {
                MessageBox.Show("Попытка сохранить файл в корневом хранилище.");
                return 1;
            }
            return 0;
        }

        private void Application_DocumentActivated(IXDocument doc)
        {
            if (doc is ISwDocument3D swDoc3D)
            {
                swDoc3D.Destroyed += SwDoc3D_Destroyed;
                _viewModelCache.GetOrCreate(swDoc3D, d => _viewModelFactory.CreateComponent(d));
            }
        }
        private void SwDoc3D_Destroyed(IXDocument doc)
        {
            if (doc is ISwDocument3D && _viewModelCache.Count > 0)
            {
                _viewModelCache.Remove(m_Doc as ISwDocument3D);
                doc.Destroyed -= SwDoc3D_Destroyed;
            }
        }
        private int OnFileSaveAsNotify2(string FileName)
        {
            var oldSwDoc = m_AddIn.Application.Documents.First(x => x.Path == FileName);
            var extension = Path.GetExtension(FileName);
            swDocumentTypes_e docType = swDocumentTypes_e.swDocNONE;

            switch (extension.ToUpper())
            {
                case ".SLDASM":
                docType = swDocumentTypes_e.swDocASSEMBLY;
                    break;
                case ".SLDPRT":
                docType = swDocumentTypes_e.swDocPART;
                    break;
                default:
                break;
            }
            
            var askDialog = MessageBox.Show(
                $"Вы использовали команду 'сохранить как'.\nОчистить идентификаторы?",
                "Сохранить как",
                MessageBoxButtons.YesNoCancel);
            if (askDialog == DialogResult.Yes)
            {
                SaveFileDialog _sfd = new SaveFileDialog();
                _sfd.FileName = Path.GetFileName(FileName);
                _sfd.DefaultExt = extension;
                _sfd.Filter = "Детали (*.SLDPRT)|*.SLDPRT | Сборки (*.SLDASM)|*.SLDASM";

                DialogResult _saveDialogResult = _sfd.ShowDialog();
                if (_saveDialogResult == DialogResult.OK)
                {
                    oldSwDoc.Close();

                    var newFile = _sfd.FileName;
                    File.Copy(FileName, newFile, true);
                    File.SetAttributes(newFile, FileAttributes.Normal);

                    try
                    {
                        var compDoc = m_AddIn.Application.Sw.OpenDoc(newFile, (int)docType);
                        ISwDocument3D swDoc = m_AddIn.Application.Documents.Active as ISwDocument3D;

                        if (swDoc != null)
                        {
                            var vm = _viewModelCache.GetOrCreate(swDoc, d => _viewModelFactory.CreateComponent(d));
                            vm.Article = "";
                            vm.PartNumber = "";
                            vm.HashSum = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message.ToString());
                    }
                }
            }
            if (askDialog == DialogResult.No)
            {
                return 0;
            }
            if (askDialog == DialogResult.Cancel)
            {
                return 1;
            }

            return 1;
        }
        protected override void Dispose(bool disposing)
        {
            m_AddIn.Application.Documents.DocumentActivated -= Application_DocumentActivated;

        }

        //    private int AGR_TaskPaneViewModel_FileSavePostNotify(int saveType, string FileName)
        //    {
        //        if (saveType != 1)
        //        {
        //            var newDoc = _app.Documents.PreCreateFromPath(FileName);
        //            newDoc.Commit(CancellationToken.None);
        //            if (newDoc != null)
        //            {
        //                (newDoc as ISwDocument3D).Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.Partnumber).Value = "";
        //                (newDoc as ISwDocument3D).Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.Article).Value = "";
        //                (newDoc as ISwDocument3D).Configurations.Active.Properties.AGR_TryGetProp(AGR_PropertyNames.HashSum).Value = "";
        //            }
        //        }
        //        return 1;
        //    }
    }
}