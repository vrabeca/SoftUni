namespace SimpleMVC.App.Data
{
    using Models;
    using MVC.Interfaces;
    using System;
    using System.Data.Entity;
    using System.Linq;

    public class NotesAppContext : DbContext,IDbIdentityContext
    {
        public NotesAppContext()
            : base("name=NotesAppContext")
        {
            Database.SetInitializer<NotesAppContext>(new DropCreateDatabaseIfModelChanges<NotesAppContext>());
        }

        
        public virtual DbSet<Note> Notes { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Login> Logins { get; set; }

        void IDbIdentityContext.SaveChanges()
        {
            this.SaveChanges();
        }
    }
}