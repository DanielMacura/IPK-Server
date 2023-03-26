using System.Text.RegularExpressions;
using System.Collections;
using NCalc;
using System;
using System.Collections.Generic;

namespace ipkpd
{
    internal class Evaluator
    {
        public double Evaluate(string input)
        {
            return PrefixCalculator(ABFNFormToPrefix(input));
        }
        private string ABFNFormToPrefix(string input)
        {

            while (FindIndexes(input).Item1 != -1)
            {
                input = Regex.Replace(input, @"\s+", " ");
                input = input.Trim();
                var original = input;
                var indexes = FindIndexes(input);
                Console.WriteLine("1: {0}  2:{1}   {2}  {3}", indexes.Item1, indexes.Item2, input[indexes.Item1], input[indexes.Item2]);
                input = input.Substring(indexes.Item1+1, (indexes.Item2 - indexes.Item1)-1).Trim();
                char opcode = input[0];
                input = input.Substring(1, input.Length-1).Trim();

                Console.WriteLine(input);

                int bracketCount = 0;
                int operatorCount = 0;
                foreach (var (charValue, i) in input.Select((value, i) => (value, i)))
                {
                    switch (charValue)
                    {
                        case '(':
                            bracketCount++;break;
                        case ')':
                            bracketCount--;
                            break;
                        case ' ':
                            if (bracketCount == 0)
                            {
                                operatorCount++;
                            }
                            break;
                    }
                }

                var start = original.Substring(0, indexes.Item1);
                var mid = string.Join("", Enumerable.Repeat(opcode+" ", operatorCount)) + input;
                
                var end = original.Substring(indexes.Item2 + 1, original.Length - indexes.Item2-1);

                Console.WriteLine("Start:{0}\nMid:{1}\nEnd:{2}", start,mid, end);
                input = start + mid + end;
                        

                Console.WriteLine(input);
                Console.WriteLine("--------------------------------------------------------");
            }

            Console.WriteLine(input);
            return input;
        }

        private Tuple<int, int> FindIndexes(string input)
        {
            int bracketCount = 0;
            
            int first = -1;
            int last = -1;
            bool exitLoop = false;
            foreach (var (charValue, i) in input.Select((value, i) => (value, i)))
            {
                switch (charValue)
                {
                    case '(':
                        if (bracketCount ==0)
                        {
                            first = i;
                        }
                        bracketCount++; 
                        break;
                    case ')':
                        bracketCount--;
                        if (bracketCount == 0)
                        {
                            last = i;
                            exitLoop = true;
                        }
                        break;
                }
                if (exitLoop) break;
            }
            return new Tuple<int, int>(first, last);
        }

        private double PrefixCalculator(string input)
        {
            var parserStack = new Stack<Double>();

            //Prefix Parser
            //var list = input.Replace("(", "").Replace(")", "").Split(" ");
            var list = input.Split(' ');
            foreach (var item in list.Reverse())
            {
                Console.WriteLine(item);
                if (Regex.IsMatch(item.ToString(), @"^\d+$"))
                {
                    parserStack.Push(Double.Parse(item));
                }
                else if (Regex.IsMatch(item.ToString(), @"^\s$"))
                {
                    
                }
                else
                {
                    var int1 = parserStack.Pop();
                    var int2 = parserStack.Pop();

                    var e = new Expression(int1 + " "+ item + " " + int2);
                    
                    parserStack.Push(Double.Parse(e.Evaluate().ToString() ?? throw new InvalidOperationException()));
                }
            }



            
            double result = parserStack.Peek();
            Console.WriteLine("Result: {0}", result);
            return result;
        }
    }
}
