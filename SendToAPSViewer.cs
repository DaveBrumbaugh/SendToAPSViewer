using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DllExporterNet4;

namespace SendToAPSViewer
{
    class Result
    {
        public string name { get; set; }
        public string urn { get; set; }
    }


    public class SendToAPSViewer
    {
        const int IDOK = 1;

        public static void BackgroundUpload(object arg)
        {
            ArrayList args = (ArrayList)arg;
            string sUploadFile = (string)args[0];
            long lSession = (long)args[1];
            string sDocGuid = (string)args[2];

            try
            {
                BPSUtilities.WriteLog($"Starting upload of '{sUploadFile}'");

                using (var client = new HttpClient())
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        var fileName = Path.GetFileName($"{sUploadFile}");
                        var fileStream = System.IO.File.Open($"{sUploadFile}", FileMode.Open);
                        content.Add(new StreamContent(fileStream), "file", fileName);

                        var requestUri = Environment.GetEnvironmentVariable("APS_UPLOAD_URL");
                        var request = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content };
                        var result = client.SendAsync(request).Result;

                        var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();

                        var resultObj = serializer.Deserialize<Result>(result.Content.ReadAsStringAsync().Result);

                        // BPSUtilities.WriteLog(body.urn);

                        // var resultObj = JsonConvert.DeserializeObject<Result>(result.Content.ReadAsStringAsync().Result);

                        BPSUtilities.WriteLog($"Upload complete. name: '{resultObj.name}', urn: '{resultObj.urn}'");

                        // dXJuOmFkc2sub2JqZWN0czpvcy5vYmplY3Q6N3A3Y3pvYnl2cDJnNmNtbWV2anlxZWN3aW4zMm1vbDMtYmFzaWMtYXBwL2MxYWRlYzUwLWMzNjctNGNmMC04Y2U5LTVjNzg4MWM1OGYwOC5ydnQ
                        // dXJuOmFkc2sub2JqZWN0czpvcy5vYmplY3Q6N3A3Y3pvYnl2cDJnNmNtbWV2anlxZWN3aW4zMm1vbDMtYmFzaWMtYXBwL2MxYWRlYzUwLWMzNjctNGNmMC04Y2U5LTVjNzg4MWM1OGYwOC5ydnQ

                        BPSUtilities.WriteLog($"Starting '{Environment.GetEnvironmentVariable("APS_VIEWER_URL")}#{resultObj.urn}'...");

                        System.Diagnostics.Process.Start($"{Environment.GetEnvironmentVariable("APS_VIEWER_URL")}#{resultObj.urn}");

                        if (PWWrapper.SetCurrentSession2(lSession))
                        {
                            int iProjId = 0, iDocId = 0;

                            if (PWWrapper.GetIdsFromGuidString(sDocGuid, ref iProjId, ref iDocId))
                            {
                                PWWrapper.SetAttributeValue(iProjId, iDocId, "URL", 
                                    $"{Environment.GetEnvironmentVariable("APS_VIEWER_URL")}#{resultObj.urn}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BPSUtilities.WriteLog($"{ex.Message}\n{ex.StackTrace}");
            }
        }

        [DllExport]
        public static int DocumentCommand
        (
            uint uiCount, //==>Count of documents 
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] plProjArray, //==>Project number Array
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] plDocArray //==> Document number Array
        )
        {
            if (uiCount == 1)
            {
                string sOrigGuid = PWWrapper.GetGuidStringFromIds(plProjArray[0], plDocArray[0]);

                if (1 == PWWrapper.aaApi_SelectDocument(plProjArray[0], plDocArray[0]))
                {
                    string sOutFile = string.Empty;
                    
                    if (PWWrapper.CopyOutDocument(sOrigGuid, false, PWWrapper.FetchDocumentFlags.CopyOut, ref sOutFile))
                    {
                        string sUploadFile = Path.Combine(Path.GetTempPath(), $"{BPSUtilities.GetARandomString(8)}{Path.GetExtension(sOutFile)}");

                        File.Copy(sOutFile, sUploadFile, true);

                        // Thread function
                        // public static void DoLoginAndCreate(object oDsUserPwd) { }

                        // functioning adding to ThreadManager queue
                        System.Threading.ParameterizedThreadStart paramThd = new System.Threading.ParameterizedThreadStart(BackgroundUpload);
                        System.Threading.Thread thd = new System.Threading.Thread(paramThd);
                        thd.IsBackground = true;
                        // ArrayList args = new ArrayList();
                        // args.Add(someArgument);
                        // args.Add(anotherArgument);
                        // args.Add(sUnEncryptedPassword);
                        // args.Add(bIsAdmin);
                        // args.Add(Properties.Settings.Default.FileSizeK);

                        long pSession = 0;

                        if (PWWrapper.GetCurrentSession2(out pSession))
                        {
                            ArrayList alArgs = new ArrayList();
                            alArgs.Add(sUploadFile);
                            alArgs.Add(pSession);
                            alArgs.Add(sOrigGuid);

                            ThreadManager.ManageThreads(thd, alArgs);
                        }

                        MessageBox.Show($"Submitted '{Path.GetFileName(sOutFile)}' to APS for viewing.", "Send to APS", MessageBoxButtons.OK);
                    }
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Just select one file.");
            }

            return IDOK;
        }

    }
}
