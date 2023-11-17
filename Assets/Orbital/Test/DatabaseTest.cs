using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using UnityWinForms.System.Windows.Forms;
using Mono.Data.Sqlite;
using Orbital.Core;
using Orbital.Core.Serialization.Sqlite;
using Orbital.Factories;
using Orbital.Serialization.SqlModel;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityWinForms.Controls;
using UnityWinForms.Examples;
using UnityWinForms.Examples.Panels;
using UnityWinForms.System.Drawing;
using Zenject;
using Component = Orbital.Serialization.SqlModel.Component;
using Object = Orbital.Serialization.SqlModel.Object;

namespace Orbital.Test
{
    public class DatabaseTest : MonoBehaviour
    {
        public World targetWorld;
        [FilePath] public string path; 
        public string commandText;
        public string insertCommandText;
        public WorldSet worldSet; 
        
        public Declaration declaration;
        [Button]
        private void Test()
        {
        }
        
        [Button]
        void Write()
        {
            using (var connection = new SqliteConnection("Data Source=DB/sqliteDb.db"))
            {
                connection.Open();
                connection.CreateTable<Serialization.SqlModel.Player>("Players", declaration);
                connection.CreateTable<Object>("Objects", declaration);
                connection.CreateTable<Component>("Components", declaration);
                connection.CreateTable<Celestial>("Celestials", declaration);
            }
        }

        [Button]
        void TestCelestials()
        {
            /*using (var connection = new SqliteConnection("Data Source=DB/sqliteDb.db"))
            {
                connection.Open();
                Worlds = connection.GetTable<Core.Serialization.SqlModel.PlayerCharacter>(declaration);
            }*/
            worldSet = new WorldSet(declaration, "Data Source=DB/sqliteDb.db");
            worldSet.WriteWorld(targetWorld);
        }
        
        [Button]
        void TestWorldLoad()
        {
            /*using (var connection = new SqliteConnection("Data Source=DB/sqliteDb.db"))
            {
                connection.Open();
                Worlds = connection.GetTable<Core.Serialization.SqlModel.PlayerCharacter>(declaration);
            }*/
            DiContainer container = new DiContainer();
            container.BindInterfacesTo<StaticBodyFactory>().FromInstance(new StaticBodyFactory());
            worldSet = new WorldSet(declaration, "Data Source=DB/sqliteDb.db");
            container.Inject(worldSet);
            worldSet.LoadWorld();
        }
        
        [Button]
        private void InsertCommand()
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + path))
            {
                connection.Open();
                using (SqliteCommand command = new SqliteCommand(commandText, connection))
                {
                    var answer = command.ExecuteScalar();
                    if(answer != null) Debug.Log(answer.ToString());
                    /*SqliteDataAdapter adapter = new SqliteDataAdapter(command);
                    adapter.Update()
                    adapter.Dispose();*/
                }
            }
        }
    }
    

    public class ViewForm : Form
    {

        public ViewForm(DataTable table)
        {
            InitializeComponent();
            for (int i = 0; i < table.Columns.Count; i++)
            {
                Debug.Log("Column: " + table.Columns[i].ColumnName);

                _tableView.Columns.Add(table.Columns[i].ColumnName, table.Columns[i].ColumnName);
                _tableView.Columns[i].Width = 100;
            }
            for (int i = 0; i < table.Rows.Count + 1; i++)
            {
                _tableView.Rows.Add();
            }
            for (int i = 0; i < table.Columns.Count; i++)
            {
                for (int j = 0; j < table.Rows.Count; j++)
                {
                    string content = table.Rows[j][i].ToString();
                    Debug.Log(content);
                    _tableView.Rows[j].ItemsControls[i] = new Label(){Text = content};
                }
            }
            _tableView.Refresh();
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            _tableView = this.Create<TableView>(8, 158);
            _tableView.Size = new Size(360, 240);
            Controls.Add(_tableView);
            SuspendLayout();

            BackColor = SystemColors.Control;
            ClientSize = new Size(365, 245);
            Location = new Point(15, 15);
            Name = "ViewForm";
            ResumeLayout(false);
            Disposed += OnDisposed;
        }


        private void OnDisposed(object sender, EventArgs e)
        {
        }

        private TableView _tableView;
    }
}