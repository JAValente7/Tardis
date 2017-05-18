using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using Black.FX;
using Black.Portal;
using Black.Surfaces;
using Black.Time;
using Microsoft.CSharp;

namespace Tardis
{
  class Program
  {

    static void Main(string[] args)
    {
      var server = new Server();

      var startDate = new DateTime(2016, 1, 4, 17, 00, 00);
      var endDate = new DateTime(2017, 1, 1);

      var book = new Book(server, 6, startDate);

      var trades = new List<FxContract>
            {
              new FxContract(book.Surface, Contract.Call, "2w", "40d", 25000000,true, false),
              new FxContract(book.Surface, Contract.Put, "1m", "20d", -50000000, true, false),
            };

      var value = book.CalculateTrades(trades, book.Surface.PublishDate, book.Surface.PublishSpot, true);

      trades.Add(new FxContract(book.Surface, Contract.Forward, "2w", "", -value[0].Value, true, false));

      book.CalculateTrades(trades, book.Surface.PublishDate, book.Surface.PublishSpot, false);

      var log = "";

      for (var currentDate = startDate; currentDate < endDate; currentDate = currentDate.AddDays(1))
      {
        if (!ContractDates.IsValidDate(currentDate, book.Surface.Events, ContractDates.ContractDateType.Expiry)) continue;

        book = new Book(server, 6, currentDate);

        var spot = book.Surface.PublishSpot;

        var greeks = book.CalculateTrades(trades, book.Surface.PublishDate, spot, true);

        log += currentDate.ToShortDateString() + "\t" + spot + "\t" + greeks[0].Value + "\t" + greeks[1].Value + "\t" + greeks[3].Value + "\t" + greeks[4].Value + "\t" + greeks[10].Value + "\n";







        PerformExpiries(book, trades, currentDate, spot);

        RollPremiums(book, trades, currentDate, spot);

        RollForwards(book, trades, currentDate, spot);


















        trades.Add(new FxContract(book.Surface, Contract.Call, "2w", "40d", 25000000, true, false));
        trades.Add(new FxContract(book.Surface, Contract.Put, "1m", "20d", -50000000, true, false));

        greeks = book.CalculateTrades(trades, book.Surface.PublishDate, book.Surface.PublishSpot, true);

        trades.Add(new FxContract(book.Surface, Contract.Forward, "2w", "", -greeks[0].Value, true, false));

        book.CalculateTrades(trades, book.Surface.PublishDate, book.Surface.PublishSpot, false);
      }



      Console.ReadLine();
    }
    private static void PerformExpiries(Book book, List<FxContract> trades, DateTime currentDate, double spot)
    {
      var expiringTrades = trades.Where(t => t.Contract != Contract.Forward && t.ContractDates.ExpiryDate.Date <= currentDate.Date).ToList();

      if (expiringTrades.Count <= 0) return;

      var newForwards = new List<FxContract>();

      foreach (var trade in expiringTrades)
      {
        if (trade.Contract == Contract.Call && spot > trade.Strike)
          newForwards.Add(new FxContract(book.Surface, Contract.Forward, "0d", trade.Strike.ToString(), (int)trade.TradeDirection * trade.Notional, true, false));

        if (trade.Contract == Contract.Put && spot < trade.Strike)
          newForwards.Add(new FxContract(book.Surface, Contract.Forward, "0d", trade.Strike.ToString(), -(int)trade.TradeDirection * trade.Notional, true, false));

        //        trades.Remove(trade);
      }

      if (newForwards.Count <= 0) return;

      book.CalculateTrades(newForwards, book.Surface.PublishDate, book.Surface.PublishSpot, false);
      trades.AddRange(newForwards);
    }

    private static void RollPremiums(Book book, List<FxContract> trades, DateTime currentDate, double spot)
    {
      var optionPremiums = trades.Where(t => t.PremValueDate.Date == ContractDates.IncrementDays(currentDate, book.CurrencyPair.TratePlus, book.Surface.Events, ContractDates.ContractDateType.Settle)).ToList();

      var netNonUsdPremium = optionPremiums.Where(o => o.PremiumCurrency.Id != 1).Sum(o => o.Premium);

      if (netNonUsdPremium != 0)
      {
        var newForwards = new List<FxContract>();

        if (book.CurrencyPair.ForignCurrency.Id != 1)
        {
          newForwards.Add(new FxContract(book.Surface, Contract.Forward, "0d", spot.ToString(), -netNonUsdPremium, true, false));
        }
        else
        {
          newForwards.Add(new FxContract(book.Surface, Contract.Forward, "0d", spot.ToString(), netNonUsdPremium / spot, true, false));
        }

        book.CalculateTrades(newForwards, book.Surface.PublishDate, book.Surface.PublishSpot, false);
        trades.AddRange(newForwards);

      }
    }


    private static void RollForwards(Book book, List<FxContract> trades, DateTime currentDate, double spot)
    {
      var forwards = trades.Where(t => t.Contract == Contract.Forward && t.ContractDates.ValueDate.Date <= currentDate.Date).ToList();

      if (forwards.Count > 0)
      {
        foreach (var trade in forwards)
        {
          if (trade.Contract == Contract.Call && spot > trade.Strike)
            trades.Add(new FxContract(book.Surface, Contract.Forward, "0d", trade.Strike.ToString(), (int)trade.TradeDirection * trade.Notional, true, false));

          if (trade.Contract == Contract.Put && spot < trade.Strike)
            trades.Add(new FxContract(book.Surface, Contract.Forward, "0d", trade.Strike.ToString(), -(int)trade.TradeDirection * trade.Notional, true, false));

          trades.Remove(trade);
        }
      }
    }

    private static void LoadBookPriceTrades()
    {
      var server = new Server();

      var book = new Book(server, 6, new DateTime(2017, 3, 3, 17, 00, 00));

      var trades = new List<FxContract>
            {
              new FxContract(book.Surface, Contract.Call, "1m", "0.75912493042775142", 10000000,true, false),
              new FxContract(book.Surface, Contract.Put, "1m", "0.75912493042775142", -10000000, true, false),
              new FxContract(book.Surface, Contract.Forward, "1m", "", -10000000, true, false)
            };


      var t = book.CalculateTrades(trades, book.Surface.PublishDate, book.Surface.PublishSpot, true);
    }

    private static void RunTimeCompile()
    {
      string code = @"
          using System;

          namespace First
          {
              public class Program
              {
                  public static void Main()
                  {
                  " +
                      "Console.WriteLine(\"Hello, world!\");"
                      + @"
                  }
              }
          }
      ";

      var provider = new CSharpCodeProvider();
      var parameters = new CompilerParameters();

      // Reference to System.Drawing library
      parameters.ReferencedAssemblies.Add("System.Drawing.dll");
      // True - memory generation, false - external file generation
      parameters.GenerateInMemory = true;
      // True - exe file generation, false - dll file generation
      parameters.GenerateExecutable = true;

      var results = provider.CompileAssemblyFromSource(parameters, code);

      if (results.Errors.HasErrors)
      {
        var stringBuilder = new StringBuilder();

        foreach (CompilerError error in results.Errors)
        {
          stringBuilder.AppendLine($"Error ({error.ErrorNumber}): {error.ErrorText}");
        }

        throw new InvalidOperationException(stringBuilder.ToString());
      }

      var assembly = results.CompiledAssembly;
      var program = assembly.GetType("First.Program");
      var main = program.GetMethod("Main");

      main.Invoke(null, null);
    }
  }
}
