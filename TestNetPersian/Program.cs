using System;
using System.Globalization;

class Program {
    static void Main() {
        var cal = new PersianCalendar();
        for (int year = 1400; year <= 1420; year++) {
            bool leap = cal.IsLeapYear(year);
            var nowruz = cal.ToDateTime(year, 1, 1, 0, 0, 0, 0, PersianCalendar.PersianEra);
            Console.WriteLine($"{year}: leap={leap}, Nowruz={nowruz:yyyy-MM-dd}");
        }
    }
}
