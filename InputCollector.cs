using ConsoleApp2;

namespace ConsoleApp3
{
    public enum ReadStatus
    {
        Read = 1,
        Unread = 2,
    }
    public enum Completion
    {
        Completed = 1,
        Pending = 2
    }
    public enum Priority
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

    public class InputCollector
    {

        public string Prompt(string label)
        {
            Console.Write(label);
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }

        public string PromptRequired(string label)
        {
            while (true)
            {
                var val = Prompt(label);
                if (!string.IsNullOrWhiteSpace(val)) return val;
                Console.WriteLine("Value required. Try again.");
            }
        }

        public string? PromptNullable(string label)
        {
            var v = Prompt(label);
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        public int PromptInt(string label)
        {
            while (true)
            {
                var v = Prompt(label);
                if (int.TryParse(v, out var n)) return n;
                Console.WriteLine("Invalid number. Try again.");
            }
        }
        public int PromptIntRead(string label)
        {
            while (true)
            {
                var v = Prompt(label);
                if (int.TryParse(v, out var n) && n is >= 1900 and <= 2025) return n;
                Console.WriteLine("Invalid number. Try again.");
            }
        }
        public int? PromptIntReadNullable(string label)
        {
            while (true)
            {
                var v = Prompt(label);
                if (string.IsNullOrWhiteSpace(v)) return null;
                if (int.TryParse(v, out var n) && n is >= 2000 and <= 2020) return n;
                Console.WriteLine("Invalid number. Try again.");
            }
        }

        public DateTime PromptDate(string label)
        {
            while (true)
            {
                var v = Prompt(label);
                if (DateTime.TryParse(v, out var dt)) return dt.Date;
                Console.WriteLine("Invalid date. Use yyyy-MM-dd or other recognizable formats.");
            }
        }

        public DateTime? PromptDateNullable(string label)
        {
            var v = Prompt(label);
            if (string.IsNullOrWhiteSpace(v)) return null;
            if (DateTime.TryParse(v, out var dt)) return dt.Date;
            Console.WriteLine("Invalid date format. Ignoring value.");
            return null;
        }

        public Priority PromptPriority()
        {
            while (true)
            {
                Console.WriteLine("Select priority: 1) Low  2) Medium  3) High");
                var v = Prompt("Choice: ");
                if (int.TryParse(v, out var n) && Enum.IsDefined(typeof(Priority), n)) return (Priority)n;
                Console.WriteLine("Invalid choice.");
            }
        }

        public Priority? PromptPriorityNullable(Priority current)
        {
            Console.WriteLine($"Current priority: {current}");
            Console.WriteLine("Select new priority or leave empty to keep");
            var v = Prompt("Choice (1-3): ");
            if (string.IsNullOrWhiteSpace(v)) return null;
            if (int.TryParse(v, out var n) && Enum.IsDefined(typeof(Priority), n)) return (Priority)n;
            Console.WriteLine("Invalid choice. Keeping current.");
            return null;
        }
        
        public Completion PromptCompletion()
        {
            while (true)
            {
                Console.WriteLine("Select priority: 1) Completion  2) Pending");
                var v = Prompt("Choice: ");
                if (int.TryParse(v, out var n) && Enum.IsDefined(typeof(Completion), n)) return (Completion)n;
                Console.WriteLine("Invalid choice.");
            }
        }
        public ReadStatus PromptReadStatus()
        {
            while (true)
            {
                Console.WriteLine("Select priority: 1) Read  2) Unread");
                var v = Prompt("Choice: ");
                if (int.TryParse(v, out var n) && Enum.IsDefined(typeof(ReadStatus), n)) return (ReadStatus)n;
                Console.WriteLine("Invalid choice.");
            }
        }
        
        public ReadStatus? PromptReadStatusNullable(ReadStatus status)
        {
            Console.WriteLine($"Current priority: {status}");
            Console.WriteLine("Select new priority or leave empty to keep");
            var v = Prompt("Choice (1 or 2): ");
            if (string.IsNullOrWhiteSpace(v)) return null;
            if (int.TryParse(v, out var n) && Enum.IsDefined(typeof(ReadStatus), n)) return (ReadStatus)n;
            Console.WriteLine("Invalid choice. Keeping current.");
            return null;
        }
        
        
        public Completion? PromptCompletionNullable(Completion status)
        {
            Console.WriteLine($"Current priority: {status}");
            Console.WriteLine("Select new priority or leave empty to keep");
            var v = Prompt("Choice (1 or 2): ");
            if (string.IsNullOrWhiteSpace(v)) return null;
            if (int.TryParse(v, out var n) && Enum.IsDefined(typeof(Completion), n)) return (Completion)n;
            Console.WriteLine("Invalid choice. Keeping current.");
            return null;
        }

        public bool? PromptBoolNullable(string label)
        {
            var v = Prompt(label);
            if (string.IsNullOrWhiteSpace(v)) return null;
            if (v.Equals("y", StringComparison.OrdinalIgnoreCase) || 
                v.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (v.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
            Console.WriteLine("Invalid input, (y or n).");
            return null;
        }
    }
}