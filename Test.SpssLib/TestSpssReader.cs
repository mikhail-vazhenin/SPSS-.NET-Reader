﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SpssLib.DataReader;

namespace Test.SpssLib
{
    [TestClass]
    public class TestSpssReader
    {

        [TestMethod]
        public void TestReadMetadata()
        {
            //var filename = @"C:\Users\ttbiagif\Documents\Datasets\Demo_set.sav";
            var filename = @"C:\Users\ttbiagif\Documents\Datasets\tests\LongStringVar.sav";
            FileStream fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            SpssReader spssDataset = new SpssReader(fileStream);
            
            var variables = spssDataset.Variables;
            foreach (var variable in variables)
            {
                Debug.WriteLine("{0} - {1}",variable.Name, variable.Label);
                foreach (KeyValuePair<double, string> label in variable.ValueLabels)
                {
                    Debug.WriteLine(" {0} - {1}", label.Key, label.Value);
                }
            }

            foreach (var record in spssDataset.Records)
            {
                foreach (var variable in variables)
                {
                    Debug.Write(record.GetValue(variable));
                    Debug.Write('\t');
                }
                Debug.WriteLine("");
            }

            fileStream.Close();
        }
    }
}
