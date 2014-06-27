﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNLP.Tools.SentenceDetect;
using OpenNLP.Tools.Tokenize;
using OpenNLP.Tools.Util;
using SharpEntropy.IO;

namespace Trainer
{
    class Program
    {
        private static readonly string CurrentDirectory = Environment.CurrentDirectory + "/../../";

        static void Main(string[] args)
        {
            var tokenizeTrainingFileDirectory = CurrentDirectory + "Input/Tokenize/";
            // train tokenizer
            const char splitMarker = '|';

            var iterations = 20;
            var cut = 1;
            var allTrainingFiles = Directory.GetFiles(tokenizeTrainingFileDirectory);

            Console.WriteLine("Starting training...");
            var model = MaximumEntropyTokenizer.Train(allTrainingFiles, iterations, cut);
            var tokenizer = new MaximumEntropyTokenizer(model);

            // test data
            var trainingLines = new List<string>();
            foreach (var trainingFile in allTrainingFiles)
            {
                trainingLines.AddRange(File.ReadAllLines(trainingFile));
            }
            var testData = new List<TokenizerTestData>();
            foreach (var trainingLine in trainingLines)
            {
                var testDataPoint = new TokenizerTestData(trainingLine, splitMarker.ToString());
                testData.Add(testDataPoint);
            }
            var results = tokenizer.RunAgainstTestData(testData);
            Console.WriteLine("Accuracy of model iteration={0}, cut={1}: {2}", iterations, cut, results.GetAccuracy());

            /*// tests
            Console.WriteLine("Running tests...");
            var tokenizer = new MaximumEntropyTokenizer(model);
            var tests = new List<string>()
            {
                "It was built of a bright brick throughout; its skyline was fantastic, and even its ground plan was wild.",
            };
            foreach (var test in tests)
            {
                Console.WriteLine(test);
                var tokens = tokenizer.Tokenize(test);
                Console.WriteLine(string.Join("|", tokens));
            }*/

            // Persisting model
            var outputFilePath = CurrentDirectory + "Output/Tokenize.nbin";
            Console.WriteLine("Persisting model in file '{0}'", outputFilePath);
            new BinaryGisModelWriter().Persist(model, outputFilePath);

            Console.WriteLine("OK");
            Console.ReadKey();
        }

        private static void OptimizeTokenizerTraining()
        {
            var tokenizeTrainingFileDirectory = CurrentDirectory + "Input/Tokenize/";
            // train tokenizer
            const char splitMarker = '|';

            foreach (var iterations in new List<int>() { 1, 5, 10, 20 })
            {
                foreach (var cut in new List<int>() { 1, 2, 5 })
                {
                    //var iterations = 10;
                    //var cut = 1;
                    var allTrainingFiles = Directory.GetFiles(tokenizeTrainingFileDirectory);

                    Console.WriteLine("Starting training...");
                    var model = MaximumEntropyTokenizer.Train(allTrainingFiles, iterations, cut);
                    var tokenizer = new MaximumEntropyTokenizer(model);

                    // test data
                    var trainingLines = new List<string>();
                    foreach (var trainingFile in allTrainingFiles)
                    {
                        trainingLines.AddRange(File.ReadAllLines(trainingFile));
                    }
                    var testData = new List<TokenizerTestData>();
                    foreach (var trainingLine in trainingLines)
                    {
                        var testDataPoint = new TokenizerTestData(trainingLine, splitMarker.ToString());
                        testData.Add(testDataPoint);
                    }
                    var results = tokenizer.RunAgainstTestData(testData);
                    Console.WriteLine("Accuracy of model iteration={0}, cut={1}: {2}", iterations, cut, results.GetAccuracy());
                }
            }
        }


        private static void OptimizeSentenceDetectionTraining()
        {
            // all directories in Input folder
            var inputFolderPath = CurrentDirectory + "Input/";
            var allDirectories = Directory.GetDirectories(inputFolderPath);
            Console.WriteLine("Pick the model to train:");
            for (var i = 0; i < allDirectories.Length; i++)
            {
                Console.WriteLine("{0} - {1}", i, Path.GetFileName(allDirectories[i]));
            }

            // read directory chosen by user
            int directoryIndexPicked = LoopUntilValidUserInput(input => int.Parse(input),
                i => i < allDirectories.Length, string.Format("Please enter a number in [0..{0}]", allDirectories.Length - 1));

            // read user parameters
            Console.WriteLine("Enter the iteration values to test, separated by a comma (ex: 10,100,200)");
            var iterations = LoopUntilValidUserInput(input => input.Split(',').Select(s => int.Parse(s.Trim())).ToList(),
                li => li != null && li.Any(),
                "At least one iteration value is required");
            Console.WriteLine("Enter the cut values to test, separated by a comma (ex: 1,2,5)");
            var cuts = LoopUntilValidUserInput(input => input.Split(',').Select(s => int.Parse(s.Trim())).ToList(),
                li => li != null && li.Any(),
                "At least one cut value is required");

            // train model file
            var directory = allDirectories[directoryIndexPicked];
            var allTrainFiles = Directory.GetFiles(directory, "*.train");
            var endOfSentenceScanner = new CharactersSpecificEndOfSentenceScanner('.', '?', '!', '"', '-');
            Console.WriteLine("Training model with files {0}", string.Join(", ", allTrainFiles.Select(f => Path.GetFileNameWithoutExtension(f))));

            // load training data
            var allSentences = new List<string>();
            foreach (var file in allTrainFiles)
            {
                allSentences.AddRange(File.ReadAllLines(file));
            }
            var testData = string.Join(" ", allSentences);

            var bestIterationValue = iterations.First();
            var bestCutValue = iterations.First();
            var bestAccuracy = 0.0d;
            foreach (var iteration in iterations)
            {
                foreach (var cut in cuts)
                {
                    var model = MaximumEntropySentenceDetector.TrainModel(allTrainFiles, iteration, cut, endOfSentenceScanner);
                    // compute accuracy
                    var sentenceDetector = new MaximumEntropySentenceDetector(model);
                    var results = sentenceDetector.SentenceDetect(testData);

                    // not perfect for comparing files but it gives a very good approximation
                    var commonValues = allSentences.Intersect(results).Count();
                    var accuracyScore = (float)commonValues / (allSentences.Count + results.Count());
                    Console.WriteLine("Accuracy for iteration={0} and cut={1}: {2}", iteration, cut, accuracyScore);
                    if (accuracyScore > bestAccuracy)
                    {
                        bestAccuracy = accuracyScore;
                        bestIterationValue = iteration;
                        bestCutValue = cut;
                    }
                }
            }

            // Persit model
            var outputFilePath = CurrentDirectory + "Output/" + Path.GetFileName(directory) + ".nbin";
            Console.WriteLine("Persisting model for iteration={0} and cut={1} to file '{2}'...", bestIterationValue, bestCutValue, outputFilePath);
            var bestModel = MaximumEntropySentenceDetector.TrainModel(allTrainFiles, bestIterationValue, bestCutValue, endOfSentenceScanner);
            new BinaryGisModelWriter().Persist(bestModel, outputFilePath);
            Console.WriteLine("Output file written.");
        }
        
        private static T LoopUntilValidUserInput<T>(Func<string, T> valueExtractionFunction, Func<T, bool> valueCheckFunction, string invalidInputMessage)
        {
            var input = Console.ReadLine();

            var value = valueExtractionFunction(input);

            if (valueCheckFunction(value))
            {
                return value;
            }
            else
            {
                Console.WriteLine(invalidInputMessage);
                return LoopUntilValidUserInput(valueExtractionFunction, valueCheckFunction, invalidInputMessage);
            }
        }
    }
}
