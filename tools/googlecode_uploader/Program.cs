using System;
using System.Collections.Generic;
using System.Text;
using Ionic.Zip;
using System.IO;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.Net;

namespace googlecode_uploader
{
    class Program
    {
        static readonly string BOUNDARY = "_GOOGLECODE_UPLOADER_BOUNDARY_";
        static Dictionary<string, string> properties = null;

        static public string ConfigFileName
        {
            get { return Process.GetCurrentProcess().MainModule.FileName + "config"; }
        }

        static string getProperty(string name)
        {
            if (properties == null)
            {
                properties = new Dictionary<string, string>();
                try
                {
                    string[] lines = File.ReadAllLines(ConfigFileName);
                    foreach (string line in lines)
                        if (line.Contains("="))
                        {
                            int pos = line.IndexOf('=');
                            properties[line.Remove(pos)] = line.Substring(pos + 1);
                        }
                }
                catch
                {
                }
            }

            if (!properties.ContainsKey(name))
            {
                Console.Write(name + ": ");
                properties[name] = Console.ReadLine();

                try
                {
                    List<string> lines = new List<string>();
                    foreach (KeyValuePair<string, string> kvp in properties)
                        lines.Add(kvp.Key + "=" + kvp.Value);

                    File.WriteAllLines(ConfigFileName, lines.ToArray());
                }
                catch
                {
                }
            }

            return properties[name];
        }
        static Uri getUploadUrl(string projectName)
        {
            return new Uri(string.Format("https://{0}.googlecode.com/files", projectName));
        }
        static string getAuthToken(string userName, string password)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:{1}", userName, password)));
        }
        static void writeToStream(Stream dest, string str)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(str);

            dest.Write(buffer, 0, buffer.Length);
        }
        static void upload(string filename, int revision)
        {
            string projectName = getProperty("Project name");
            string userName = getProperty("Username");
            string password = getProperty("Password");
            string summary = getProperty("Summary").Replace("[revision]", revision.ToString());
            string[] labels = getProperty("Labels").Split(", ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            if (userName.EndsWith("@gmail.com"))
                userName = userName.Remove(userName.Length - "@gmail.com".Length);

            Uri uploadUri = getUploadUrl(projectName);

            HttpWebRequest req = WebRequest.Create(uploadUri) as HttpWebRequest;

            req.Method = "POST";
            req.ContentType = "multipart/form-data; boundary=" + BOUNDARY;
            req.UserAgent = "googlecode_uploader";

            req.Headers.Add(HttpRequestHeader.Authorization,
                string.Format("Basic {0}", getAuthToken(userName, password)));

            using (Stream reqStream = req.GetRequestStream())
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine(string.Format("--{0}", BOUNDARY));
                sb.AppendLine("Content-Disposition: form-data; name=\"summary\"");
                sb.AppendLine();
                sb.AppendLine(summary);

                foreach (string label in labels)
                {
                    sb.AppendLine(string.Format("--{0}", BOUNDARY));
                    sb.AppendLine("Content-Disposition: form-data; name=\"label\"");
                    sb.AppendLine();
                    sb.AppendLine(label);
                }

                sb.AppendLine(string.Format("--{0}", BOUNDARY));
                sb.AppendLine(string.Format("Content-Disposition: form-data; " +
                              "name=\"filename\"; filename=\"{0}\"", filename));
                sb.AppendLine("Content-Type: application/octet-stream");
                sb.AppendLine();

                writeToStream(reqStream, sb.ToString());

                using (FileStream f = File.OpenRead(filename))
                {
                    int bufferSize = 4096;
                    byte[] buffer = new byte[bufferSize];
                    int count = 0;

                    while ((count = f.Read(buffer, 0, bufferSize)) > 0)
                    {
                        reqStream.Write(buffer, 0, count);
                    }
                }

                sb = new StringBuilder();

                sb.AppendLine();
                sb.AppendLine(string.Format("--{0}--", BOUNDARY));

                writeToStream(reqStream, sb.ToString());
            }

            try
            {
                using (HttpWebResponse res = req.GetResponse() as HttpWebResponse)
                {
                    Console.WriteLine("Response: " + res.StatusCode.ToString());
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        static void Main(string[] args)
        {
            string startDir = Directory.GetCurrentDirectory();
            string svnFile = Path.Combine(Path.Combine(startDir, ".svn"), "entries");

            string[] svnData = File.ReadAllLines(svnFile);
            int revision = int.Parse(svnData[3]);

            string uploadFilename = Path.Combine(startDir, getProperty("Download name") + "_r" + revision.ToString() + ".zip");
            ZipFile uploadFile = new ZipFile();
            Regex filter = new Regex(getProperty("Filter"));
            foreach (string filename in Directory.GetFiles(startDir))
                if (filter.Match(filename).Success)
                    uploadFile.AddFile(filename, "");
            uploadFile.Save(uploadFilename);

            upload(uploadFilename, revision);

            Console.ReadKey(true);
        }
    }
}
