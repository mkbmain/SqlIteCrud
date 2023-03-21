using Microsoft.Data.Sqlite;
using Mkb.DapperRepo.Attributes;
using System.Collections.Generic;
using System.Drawing.Design;

namespace WinFormsApp2
{
    public class Form1 : Form
    {
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
        private ComboBox TableSelectorBox = new ComboBox();
        private Button Insert = new Button();
        private Panel GroupBox = new Panel();
        static SqliteConnection sqliteConnection = null;
        static Mkb.DapperRepo.Repo.SqlRepo Repo = new Mkb.DapperRepo.Repo.SqlRepo(() => sqliteConnection);
        private Dictionary<string, TableInfo> tables = new Dictionary<string, TableInfo>();

        public Form1()
        {
            Insert.Left = 55;
            Insert.Text = "Insert";
            this.SuspendLayout();
            this.Size = new System.Drawing.Size(800, 450);
            this.Load += Form1_Load;
            TableSelectorBox.SelectedValueChanged += TableSelectorBox_SelectedValueChanged;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.ShowInTaskbar = true;
            this.Controls.Add(TableSelectorBox);
            GroupBox.Text = "";
            GroupBox.Size = new Size(this.Width - 33, this.Height - 105);
            GroupBox.Location = new Point(5, 50);
            this.Controls.Add(GroupBox);
            this.ResumeLayout(false);
            Insert.Click += Insert_Click;
        }

        private void Insert_Click(object? sender, EventArgs e)
        {
            var table = GroupBox.Name;
            sqliteConnection.Open();
            tables[table].Insert(sqliteConnection).ExecuteNonQuery();
            sqliteConnection.Close();

        }

        private List<Control> _addedControls = new List<Control>();
        private void TableSelectorBox_SelectedValueChanged(object? sender, EventArgs e)
        {
            string selected = TableSelectorBox.SelectedItem.ToString() ?? "";
            foreach (Control item in _addedControls)
            {
                GroupBox.Controls.Remove(item);
            }

            GroupBox.Name = selected;
            int top = 0;
            GroupBox.AutoScroll = true;
            _addedControls = tables[selected].Controls();
            foreach (var item in _addedControls)
            {
                item.Top = top;
                top += 35;
                GroupBox.Controls.Add(item);
            }
            Insert.Top = top;
            GroupBox.Controls.Add(Insert);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var openFile = new OpenFileDialog();
            openFile.Title = "Load Sqlite db";
            if (openFile.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            tables = new Dictionary<string, TableInfo>();

            sqliteConnection = new SqliteConnection($"Data Source={openFile.FileName}");

            var tableNames = Repo.QueryMany<TableDetail>("SELECT tbl_name,sql from sqlite_master").ToArray();
            foreach (var item in tableNames)
            {
                var response = Repo.QueryMany<ColInfo>($"PRAGMA table_info({item.TableName});");

                foreach (var col in response) { col.Sql = item.Sql; }

                tables.Add(item.TableName, new TableInfo { TableName = item.TableName, ColInfos = response.ToArray() });
            }

            TableSelectorBox.Items.AddRange(tableNames.Select(w=> w.TableName).ToArray());

        }
    }
}

public class TableInfo
{
    public string TableName { get; set; }
    public ColInfo[] ColInfos { get; set; }

    private List<Control> controls = null;

    public SqliteCommand Insert(SqliteConnection connection)
    {
        var items = ColInfos.Where(q => q.Value != string.Empty).Select(w => w.Name).ToArray();
        var sql = $"insert into {TableName} ({string.Join(",", items)}) values ({string.Join(",", items.Select(w => $"@{w}"))})";
        var cmd = new SqliteCommand(sql, connection);
        foreach (var item in ColInfos.Where(q => q.Value != string.Empty))
        {
            cmd.Parameters.Add(new SqliteParameter("@" + item.Name, item.Value.ToLower() == "blank" ? "" : item.Value));
        }
        return cmd;
    }
    public List<Control> Controls()
    {
        controls ??= ColInfos.Select(w => w.WindowControl()).ToList();
        return controls;
    }
}
public class TableDetail
{
    [SqlColumnName("tbl_name")]
    public string TableName { get; set; }
    public string Sql { get; set; }
}
public record ColInfo
{
    public string Name { get; set; }
    public string Type { get; set; }

    public string Sql { get; set; }
    public bool NotNull { get; set; }

    private bool _valid = false;

    public CSharpType RealType => CalculateType();

    private Control ControlItem = new TextBox();

    public bool Valid => _valid;
    public string Value => ControlItem is CheckBox ? ((CheckBox)ControlItem).Checked.ToString() : ControlItem.Text;
    public Control WindowControl()
    {
        var panel = new Panel
        {
            Size = new Size(300, 30)
        };
        panel.Controls.Add(new Label
        {
            AutoSize = false,
            Text = Name,
            Size = new Size(85, 30),
            Location = new Point(1, 1)
        });



        if (CalculateType() == CSharpType.BOOl && ControlItem is not CheckBox)
        {
            ControlItem = new CheckBox();
        }
        ControlItem.Location = new Point(100, 1);
        ControlItem.Size = new Size(200, 30);
        ControlItem.Text = "";
        ControlItem.TextChanged += TextBox_TextChanged;
        _valid = false;
        panel.Controls.Add(ControlItem);
        if (Pk && Sql.ToLower().Contains("autoincrement"))
        {
            ControlItem.Enabled = false;
        }
        return panel;
    }

    private void TextBox_TextChanged(object? sender, EventArgs e)
    {
        switch (CalculateType())
        {
            case CSharpType.DATETIME:
            case CSharpType.DATE:
                ControlItem.BackColor = DateTime.TryParse(Value, out var _) ? Color.White : Color.Red;
                break;

            case CSharpType.INTEGER:
                ControlItem.BackColor = int.TryParse(Value, out var _) ? Color.White : Color.Red;
                break;

            case CSharpType.REAL:
                ControlItem.BackColor = decimal.TryParse(Value, out var _) ? Color.White : Color.Red;
                break;

            case CSharpType.BOOl:
                ControlItem.BackColor = bool.TryParse(Value, out var _) ? Color.White : Color.Red;
                break;
        }
    }

    public CSharpType CalculateType()
    {
        // could maybe cache this but meh
        var lower = Type.ToLower();
        if (lower.Contains("int") || lower.Contains("NUMERIC")) return CSharpType.INTEGER;
        if (lower.Contains("decimal") || lower.Contains("NUMERIC") || lower.Contains("real") || lower.Contains("double") || lower.Contains("float")) return CSharpType.REAL;
        if (lower.Contains("datetime")) return CSharpType.DATETIME;
        if (lower.Contains("date")) return CSharpType.DATE;

        if (lower.Contains("bit") || lower.Contains("boolean")) return CSharpType.BOOl;
        return CSharpType.TEXT;
    }

    public bool Pk { get; set; }
}

// https://www.sqlite.org/datatype3.html
public enum CSharpType
{
    INTEGER,
    TEXT,
    BLOB,
    REAL, // decimal
          //   NUMERIC       //we are going to cheat this, we are going to treat numeric as int , decimal as real , boolean as int , and give date\dateime there own types
    DATE,
    DATETIME,
    BOOl
}