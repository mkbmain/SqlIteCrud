using Microsoft.Data.Sqlite;
using Mkb.DapperRepo.Attributes;

namespace WinFormsApp2
{
    public class Form1 : Form
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }

        private readonly ComboBox _tableSelectorBox = new();
        private readonly Button _insert = new() { Size = new Size(65, 30) };
        private readonly Panel _groupBox = new();
        private static SqliteConnection _sqliteConnection;
        private static readonly Mkb.DapperRepo.Repo.SqlRepo Repo = new(() => _sqliteConnection);
        private Dictionary<string, TableInfo> _tables = new();

        private Form1()
        {
            _insert.Left = 55;
            _insert.Text = "Insert";
            SuspendLayout();
            Size = new Size(800, 450);
            Load += Form1_Load;
            _tableSelectorBox.SelectedValueChanged += TableSelectorBox_SelectedValueChanged;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = true;
            Controls.Add(_tableSelectorBox);
            _groupBox.Text = "";
            _groupBox.Size = new Size(Width - 33, Height - 105);
            _groupBox.Location = new Point(5, 50);
            Controls.Add(_groupBox);
            ResumeLayout(false);
            _insert.Click += Insert_Click;
        }

        private void Insert_Click(object sender, EventArgs e)
        {
            var table = _groupBox.Name;
            _sqliteConnection.Open();
            try
            {
                _tables[table].Insert(_sqliteConnection).ExecuteNonQuery();
                MessageBox.Show("Done");
                foreach (var w in _tables[table].ColInfos)
                {
                   w.ResetValue();
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }

            _sqliteConnection.Close();
            TableSelectorBox_SelectedValueChanged(sender, e);
        }

        private List<Control> _addedControls = new();

        private void TableSelectorBox_SelectedValueChanged(object sender, EventArgs e)
        {
            var selected = _tableSelectorBox.SelectedItem.ToString() ?? "";
            foreach (var item in _addedControls)
            {
                _groupBox.Controls.Remove(item);
            }

            _groupBox.Name = selected;
            var top = 0;
            _groupBox.AutoScroll = true;
            _addedControls = _tables[selected].Controls();
            foreach (var item in _addedControls)
            {
                item.Top = top;
                top += 35;
                _groupBox.Controls.Add(item);
            }

            _insert.Top = top;
            _groupBox.Controls.Add(_insert);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var openFile = new OpenFileDialog();
            openFile.Title = "Load Sqlite db";
            if (openFile.ShowDialog() != DialogResult.OK)
            {
                Application.Exit();
            }

            _tables = new Dictionary<string, TableInfo>();

            _sqliteConnection = new SqliteConnection($"Data Source={openFile.FileName}");

            var tableNames = Repo.QueryMany<TableDetail>(@"SELECT * from sqlite_master").ToArray();
            foreach (var item in tableNames)
            {
                var response = Repo.QueryMany<ColInfo>($"PRAGMA table_info({item.TableName});");

                foreach (var col in response)
                {
                    col.Sql = item.Sql;
                }

                _tables.Add(item.TableName,
                    new TableInfo { TableName = item.TableName, ColInfos = response.ToArray() });
            }

            _tableSelectorBox.Items.AddRange(tableNames.Select(w => w.TableName).ToArray());
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
        var sql =
            $"insert into {TableName} ({string.Join(",", items)}) values ({string.Join(",", items.Select(w => $"@{w}"))})";
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
    [SqlColumnName("tbl_name")] public string TableName { get; set; }
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
    public void ResetValue() => ControlItem.Text = "";
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

        if (CalculateType() == CSharpType.BOOl && ControlItem is not CheckBox) ControlItem = new CheckBox();

        ControlItem.Location = new Point(100, 1);
        ControlItem.Size = new Size(200, 30);
        ControlItem.Text = "";
        ControlItem.TextChanged += TextBox_TextChanged;
        _valid = false;
        panel.Controls.Add(ControlItem);
        ControlItem.Enabled = !(Pk && Sql.ToLower().Contains("autoincrement"));

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

    private CSharpType? cSharpType = null;

    public CSharpType CalculateType()
    {
        if (cSharpType != null) return cSharpType.Value;

        var lower = Type.ToLower();
        if (lower.Contains("int") || lower.Contains("NUMERIC"))
        {
            cSharpType = CSharpType.INTEGER;
            return cSharpType.Value;
        }

        if (lower.Contains("decimal") || lower.Contains("NUMERIC") || lower.Contains("real") ||
            lower.Contains("double") || lower.Contains("float"))
        {
            cSharpType = CSharpType.REAL;
            return cSharpType.Value;
        }

        if (lower.Contains("datetime"))
        {
            cSharpType = CSharpType.DATETIME;
            return cSharpType.Value;
        }

        if (lower.Contains("date"))
        {
            cSharpType = CSharpType.DATE;
            return cSharpType.Value;
        }

        if (lower.Contains("bit") || lower.Contains("boolean"))
        {
            cSharpType = CSharpType.BOOl;
            return cSharpType.Value;
        }

        cSharpType = CSharpType.TEXT;
        return cSharpType.Value;
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