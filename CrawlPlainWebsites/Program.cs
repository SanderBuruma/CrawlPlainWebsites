using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CrawlPlainWebsites
{
    internal class Program
    {
        #region Properties
        static List<string> domainUrls = new List<string> { "cnn.com" };
        static List<Task<string>> getTasks = new List<Task<string>>();
        static int index = 0;
        #endregion

        #region Settings
        static readonly Regex urlRgx = new Regex(@"\bhttps://(?!www)(?!.*,)(?!.*google)((?:\w+\.)?\w+\.\w{2,5}\b)(?!\.)/?");
        static readonly int maxTasks = 10;
        static readonly string filePath = "out.txt";
        #endregion

        static void Main(string[] args)
        {
            // Load progress if it exists
            if (File.Exists(filePath))
            {
                var file = File.ReadAllLines(filePath);
                index = int.Parse(file[0]);
                domainUrls = new List<string>();
                for (int i = 1; i < file.Length; i++)
                {
                    domainUrls.Add(file[i]);
                }
            }

            var asyncMain = MainAsync();

            // Wait for user to end the process            '
            while(true)
            {
                Thread.Sleep(100);
                if (asyncMain.IsFaulted)
                {
                    Console.WriteLine("asyncMain faulted");
                    Console.WriteLine(asyncMain.Exception.ToString());
                    asyncMain.Dispose();
                    asyncMain = MainAsync();
                }
                if (asyncMain.IsCompleted)
                {
                    Console.WriteLine("asyncMain completed");
                    asyncMain.Dispose();
                    Console.ReadKey();
                }
            }
        }

        private static async Task MainAsync()
        {
            RefillRequestQue();
            while (getTasks.Count > 0)
            {
                // Reduce the request to only the ones that are running
                Task<string> completedRequest = await Task.WhenAny(getTasks);
                getTasks = getTasks.Where(x => !x.IsCompleted).ToList();

                // Skip failed requests
                if (completedRequest.Status != TaskStatus.RanToCompletion)
                {
                    continue;
                }

                // Add new matches to the list
                var html = completedRequest.Result;
                completedRequest.Dispose();
                MatchCollection matches = urlRgx.Matches(html);
                IEnumerable<string> matchesSelect = matches.Cast<Match>().Select(x => x.Groups[1].Value);
                matchesSelect = matchesSelect.Distinct();
                foreach (string v in matchesSelect)
                {
                    if (!domainUrls.Contains(v))
                    {
                        domainUrls.Add(v);
                    }
                }

                RefillRequestQue();
            }
        }

        private static void SaveProgress()
        {
            domainUrls.Insert(0, (index - maxTasks).ToString());
            File.WriteAllText("out.txt", string.Join("\n", domainUrls));
            domainUrls.RemoveAt(0);
        }

        private static void RefillRequestQue()
        {
            while (index < domainUrls.Count && getTasks.Count < maxTasks)
            {
                getTasks.Add(RunRequest(domainUrls[index]));
                index++;
            }
            SaveProgress();
        }

        private static async Task<string> RunRequest(string url)
        {
            var hc = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
            var temp = await hc.GetAsync("https://" + url);
            return await temp.Content.ReadAsStringAsync();
        }
    }
}
