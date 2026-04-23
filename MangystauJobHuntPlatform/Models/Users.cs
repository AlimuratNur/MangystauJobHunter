namespace MangystauJobHuntPlatform.Models;

public class Users
{
   
        public int Id { get; set; }
        public long TelegramId { get; set; } // Для бота 
        public string Name { get; set; }
        public UserRole Role { get; set; }
        public string Skills { get; set; } // Сюда пишем навыки через запятую для AI
    }
