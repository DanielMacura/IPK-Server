using System.Text.RegularExpressions;
using System.Numerics;

namespace ipkcpd;


internal class Evaluator
{
    public BigInteger? Evaluate(string input)
    {
        try
        {
            var prefix = AbnfToNormalPolishNotation(input);
            //Console.WriteLine(prefix);
            return PrefixCalculator(prefix);
        }
        catch
        {
            return null;
        }
        
    }
    
    private static string? AbnfToNormalPolishNotation(string input)
    {
        while (FindIndexes(input).Item1 != -1)
        {
            input = Regex.Replace(input, @"\s+", " ");
            input = input.Trim();
            var original = input;
            var indexes = FindIndexes(input);


            //Console.WriteLine(indexes.ToString(), input);
            //Console.WriteLine("1: {0}  2:{1}   {2}  {3}", indexes.Item1, indexes.Item2, input[indexes.Item1], input[indexes.Item2]);
            input = input.Substring(indexes.Item1+1, (indexes.Item2 - indexes.Item1)-1).Trim();
            var opcode = input[0];
            input = input.Substring(1, input.Length-1).Trim();

            //Console.WriteLine(input);

            var bracketCount = 0;
            var operatorCount = 0;
            foreach (var (charValue, i) in input.Select((value, i) => (value, i)))
            {
                switch (charValue)
                {
                    case '(':
                        bracketCount++; break;
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

            var start = original[..indexes.Item1];
            var mid = string.Join("", Enumerable.Repeat(opcode+" ", operatorCount)) + input;

            var end = original.Substring(indexes.Item2 + 1, original.Length - indexes.Item2-1);

            //Console.WriteLine("Start:{0}\nMid:{1}\nEnd:{2}", start, mid, end);
            input = start + mid + end;


            //Console.WriteLine(input);
            //.WriteLine("--------------------------------------------------------");
        }

        //Console.WriteLine(input);
        return input;
    }


    private static Tuple<int, int> FindIndexes(string input)
    {
        var bracketCount = 0;

        var first = -1;
        var last = -1;
        var exitLoop = false;
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

    private static void PrintStack(Stack<BigInteger> s)
    {
        if (s.Count == 0)
            return;
        var x = s.Peek();
        s.Pop();
        PrintStack(s);
        //Console.Write(x + " ");
        s.Push(x);
    }


    private static BigInteger? PrefixCalculator(string? input)
    {
        if (input == null)
        {
            return null;
        }
        var parserStack = new Stack<BigInteger>();

        var list = input.Split(' ');
        while (list.Length!=0)
        {
            var item = list.Last();
            list = list.SkipLast(1).ToArray();
            //PrintStack(parserStack);
            if (Regex.IsMatch(item.ToString(), @"^\d+$"))
            {
                parserStack.Push(BigInteger.Parse(item));
            }
            else if (Regex.IsMatch(item.ToString(), @"^\s$"))
            {

            }
            else
            {
                var int1 = parserStack.Pop();
                var int2 = parserStack.Pop();

                //Console.WriteLine(int1 + " "+ item + " " + int2);

                switch (item)
                {
                    case "+":
                        parserStack.Push(int1 + int2);
                        break;
                    case "-":
                        parserStack.Push(int1 - int2);
                        break;
                    case "*":
                        parserStack.Push(int1 * int2);
                        break;
                    case "/":
                        parserStack.Push(int1 / int2);
                        break;
                    default:
                        Console.WriteLine("ERR: Unknown operator \"{0}\".", item);
                        break;
                }
            }
        }

        var result = parserStack.Peek();
        //Console.WriteLine("");
        PrintStack(parserStack);
        //Console.WriteLine("Result: {0}", result);
        return result;
    }
}