using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;
using searching.Models;
using System.Text.RegularExpressions;
using Lucene.Net.Tartarus.Snowball.Ext;
using PorterStemmer;
using PorterStemmer.Stemmers;

namespace searching.Controllers
{
    public class NewSearchController : Controller
    {
        public static List<string> URL = new List<string>();
        public static List<string> term = new List<string>();
        public static List<int> freq = new List<int>();
        public static List<List<int>> positionlist = new List<List<int>>();
        public static List<int> Doc_id = new List<int>();
        public static Dictionary<string, Dictionary<int,List<int>>> build_Dictionary_toUserSearch = new Dictionary<string, Dictionary<int,List<int>>>();
        public static Dictionary<int, Dictionary<string, List<int>>> build_Dictionary_toDocument = new Dictionary<int, Dictionary<string, List<int>>>();
        public static HashSet<int> Documents = new HashSet<int>();

        // GET: NewSearch
        public ActionResult Index()
        {

            return View();
        }
        public ActionResult ViewResults(string Search)
        {
            List<string> words_after_tokenizing = new List<string>();
            List<string> words_after_Linguistic_Modules = new List<string>();
            List<string> words_after_Stemming = new List<string>();
            List<int> result = new List<int>();
            bool Douple_Qoute = false;
            string newconn = ConfigurationManager.ConnectionStrings["myconnection"].ConnectionString;
            string user_search = Search;
            Search_from_DB();
            URL_from_DB();
            if (user_search.Contains("\""))
            {
                Douple_Qoute = true;
                user_search = user_search.Remove(0, 1);
                user_search = user_search.Remove(user_search.Length - 1, 1);

            }
            words_after_tokenizing = tokenizing(user_search);
            words_after_Linguistic_Modules = Linguistic_Modules(words_after_tokenizing);
            words_after_Stemming = stemming(words_after_Linguistic_Modules);
            Build_Dictionary(words_after_Stemming);
            if (Douple_Qoute)
            {
                result = show_document_Double_Quotes(words_after_Stemming.Count);
            }
            else
            {
                result = show_document_MultiKeyword(words_after_Stemming.Count);
            }
            List<Result> newresult = new List<Result>();
            foreach (var i in result)
            {
                Result res = new Result();
                res.URLS = URL[i - 1];
                newresult.Add(res);
            }
            return View(newresult);
        }

        public static List<int> show_document_MultiKeyword(int count)
        {
            List<int> res = new List<int>();
            for (int counter = count; counter > 0; counter--)
            {
                Dictionary<int, int> document_show = new Dictionary<int, int>();
                foreach (var i in build_Dictionary_toDocument)
                {
                    if (i.Value.Count == counter)
                    {
                        List<List<int>> list_of_pos = new List<List<int>>();
                        foreach (var x in i.Value)
                            list_of_pos.Add(x.Value);
                        int freq = Document_freq_MultiKeyword(list_of_pos);
                        document_show.Add(i.Key, freq);
                    }
                }
                document_show.OrderBy(key => key.Value);
                foreach (var i in document_show)
                    res.Add(i.Key);
            }
            return res;
        }
        public static int Document_freq_MultiKeyword(List<List<int>> list_of_pos)
        {
            int res = 0;
            for (int i = 1; i < list_of_pos.Count; i++)
            {
                res = Find_different_sequence_MultiKeyword(list_of_pos[i - 1], list_of_pos[i], res);
            }
            return res;
        }
        public static int Find_different_sequence_MultiKeyword(List<int> list1, List<int> list2, int mndistance)
        {
            int res = 100000000;
            for (int i = 0; i < list1.Count; i++)
            {
                int mn = res;
                for (int j = 0; j < list2.Count; j++)
                {
                    mn = Math.Min(Math.Abs(list1[i] - list2[j]), mn);
                }
                res = Math.Min(res, mn);
            }
            return res + mndistance;
        }

        private static List<int> show_document_Double_Quotes(int  count)
        {
            List<int> res = new List<int>();
            Dictionary<int, int> document_show = new Dictionary<int, int>();
            foreach(var i in build_Dictionary_toDocument)
            {
                if(i.Value.Count == count)
                {
                    List<List<int>> list_of_pos = new List<List<int>>();
                    foreach (var x in i.Value)
                        list_of_pos.Add(x.Value);
                    int freq = Document_freq(list_of_pos);
                    if (freq != 0)
                        document_show.Add(i.Key, freq);
                }
            }
            document_show.OrderBy(key => key.Value);
            foreach (var i in document_show)
                res.Add(i.Key);
            return res;
        }
        private static int Document_freq (List<List<int>> list_of_pos)
        { 
            List<int> newres = new List<int>();
            for(int i=1; i < list_of_pos.Count; i++)
            {
                newres = Find_different_sequence(list_of_pos[i - 1], list_of_pos[i], newres);
            }
            return newres.Count;
        }
        private static List<int> Find_different_sequence(List<int> list1 , List<int>list2 , List<int>list3 ) {
            List<int> res = new List<int>();
            if(list3.Count == 0)
            {
                for(int i=0; i<list1.Count; i++)
                {
                    for(int j=0; j < list2.Count; j++)
                    {
                        if(list2[j] - list1[i] == 1)
                        {
                            res.Add(list2[j]);
                            break;
                        }

                    }
                }
            }
            else
            {
                foreach(var i in list3)
                {
                    if (list2.Contains(i + 1))
                    {
                        res.Add(i + 1);
                    }
                }
            }
            return res;
        }
        private static void  Build_Dictionary(List<string> words) {
            Documents.Clear();
            build_Dictionary_toUserSearch.Clear();
            build_Dictionary_toDocument.Clear();
            for (int i = 0; i < words.Count; i++) {
                for (int j = 0; j < term.Count; j++) {
                    if (words[i] == term[j]) {
                        Documents.Add(Doc_id[j]);
                        if (build_Dictionary_toUserSearch.ContainsKey(words[i]) && 
                             !build_Dictionary_toUserSearch[words[i]].ContainsKey(Doc_id[j]))
                        {
                            build_Dictionary_toUserSearch[words[i]].Add(Doc_id[j], positionlist[j]);
                        }
                        else if(!build_Dictionary_toUserSearch.ContainsKey(words[i])) {
                            Dictionary<int,List<int>> list_position_DocId = new Dictionary<int,List<int>>();
                            list_position_DocId.Add(Doc_id[j], positionlist[j]);
                            build_Dictionary_toUserSearch.Add(words[i],list_position_DocId);
                        }
                    
                    }
                }
            } 
            foreach(var x in Documents)
            {
                for(int i=0; i < words.Count; i++)
                {
                    if (build_Dictionary_toUserSearch[words[i]].ContainsKey(x))
                    {
                        if (build_Dictionary_toDocument.ContainsKey(x) && 
                            ! build_Dictionary_toDocument[x].ContainsKey(words[i]))
                        {
                            build_Dictionary_toDocument[x].Add(words[i] , build_Dictionary_toUserSearch[words[i]][x]);
                        }
                        else if(!build_Dictionary_toDocument.ContainsKey(x))
                        {
                            Dictionary<string, List<int>> newdec = new Dictionary<string, List<int>>();
                            newdec.Add(words[i], build_Dictionary_toUserSearch[words[i]][x]);
                            build_Dictionary_toDocument.Add(x, newdec);
                        }
                    }
                }
            }
        }

        public void URL_from_DB()
        {
            URL.Clear();
            SqlConnection conn = new SqlConnection(@"Data Source=DESKTOP-3VCJVQM\SQLEXPRESS
                           ;Initial Catalog=SearchEngine;Integrated Security=True");
            SqlCommand cmd;
            SqlDataReader rdr = null;
            conn.Open();
            string CommandText = "select URL from URL_____content ";
            cmd = new SqlCommand(CommandText, conn);
            rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                URL.Add(rdr["URL"].ToString());
            }

            conn.Close();
        }
        public void Search_from_DB()
        {
            Doc_id.Clear();
            term.Clear();
            freq.Clear();
            positionlist.Clear();
            SqlConnection conn = new SqlConnection(@"Data Source=DESKTOP-3VCJVQM\SQLEXPRESS
                           ;Initial Catalog=SearchEngine;Integrated Security=True");
            SqlCommand cmd;
            SqlDataReader rdr = null;
            conn.Open();
            string CommandText = "select DocID,Term,Freq,Position from Terms_after_stemming ";
            cmd = new SqlCommand(CommandText, conn);
            rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                Doc_id.Add((int)rdr["DocID"]);
                term.Add(rdr["Term"].ToString());
                freq.Add((int)rdr["Freq"]);
                String positionfrom_DB = (rdr["Position"].ToString());
                string[] position = positionfrom_DB.Split(',');
                List<int> pos = new List<int>();
                for (int i = 0; i < position.Length; i++) {

                    pos.Add((Convert.ToInt32(position[i])));
                }
                positionlist.Add(pos);
            }

            conn.Close();
        }
        public static string[] SplitUpperCase(string input)
        {
            return Regex.Split(input, @"(?<!^)(?=[A-Z])");
        }
        public static List<string> tokenizing(string doc)
        {

            doc = doc.Replace('\n', ' ');
            string[] str = doc.Split(new[] { ',', ' ', ';', ':', '-', '.', '&', '©' },
                            StringSplitOptions.RemoveEmptyEntries);
            List<string> words = new List<string>();
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i].All(char.IsUpper))
                {
                    words.Add(str[i]);
                }
                else
                {
                    int upper = 0;
                    int count = str[i].Length;
                    for (int b = 0; b < count; b++)
                    {
                        char ch = str[i][b];
                        if (ch >= 'A' && ch <= 'Z')
                            upper++;
                    }
                    if (upper > 1)
                    {
                        string[] tempwords = SplitUpperCase(str[i]);
                        for (int x = 0; x < tempwords.Length; x++)
                        {
                            words.Add(tempwords[x]);
                        }
                    }
                    else
                    {
                        words.Add(str[i]);
                    }
                }
            }
            
            for (int i = 0; i < words.Count; i++)
            {
                if (words[i].Length == 1)
                {
                    words.RemoveAt(i);
                    i--;
                    continue;
                }
               
            }

            return words;
        }
        private static List<string> Linguistic_Modules(List<string> words)
        {
            string[] stopwords = {  "a", "an", "and", "are", "as", "at", "be", "but", "by", "for",
                   "if", "in", "into", "is", "it", "no", "not", "of", "on", "or", "such", "that", "the",
                   "their", "then", "there", "these", "they", "this", "to", "was", "will", "with", ".","!","©" , "™"};
            for (int i = 0; i < words.Count; i++)
            {
                words[i] = words[i].ToLower();
            }
            for (int i = 0; i < words.Count; i++)
            {
                for (int j = 0; j < stopwords.Length; j++)
                {
                    if (words[i] == stopwords[j])
                    {
                        words.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }
            return words;
        }
        public static List<string> stemming(List<string> words)
        {
            
            for (int i = 0; i < words.Count; i++)
            {
                try
                {
                   
                    words[i] = PorterStemmer.Porter.GetStem(words[i]);
                   
                }
                catch (Exception e)
                {
                    words.RemoveAt(i);
                    i--;
                }
            }
            return words;
        }

    }
}