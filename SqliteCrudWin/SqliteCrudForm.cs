using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace SqliteCrud
{
    public class SqliteCrudForm : Form
    {
        private static SqliteConnection _sqliteConnection;
        private static bool Loading = false;
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new SqliteCrudForm());
        }

        private readonly ComboBox _tableSelectorBox = new() { Visible = false };
        private readonly Button _insertBtn = new() { Size = new Size(85, 30) };
        private readonly Button _updateBtn = new() { Size = new Size(85, 30) };
        private readonly Button _deleteBtn = new() { Size = new Size(85, 30) };
        private readonly Panel _groupBox = new();
        private readonly TextBox _dbFilePathTextBox = new TextBox { ReadOnly = true, Left = 205, Multiline = false, Size = new Size(300, 30), Text = "Click Me To Open Db" };
        private readonly DataGridView _dataGridView = new() { ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, Size = new Size(450, 1), Location = new Point(310, 1), MultiSelect = false };

        private List<Control> _addedControls = new();
        private Dictionary<string, TableInfo> _tables = new();

        private SqliteCrudForm()
        {
            Text = "MKB Sqlite viewer";
            _insertBtn.Left = 35;
            _insertBtn.Text = "Insert";
            _updateBtn.Text = "Update";
            _deleteBtn.Text = "Delete";
            _updateBtn.Left = _insertBtn.Right + 5;
            _deleteBtn.Left = _updateBtn.Right + 5;
            Size = new Size(800, 450);
            _tableSelectorBox.SelectedValueChanged += TableSelectorBox_SelectedValueChanged;
            Controls.Add(_tableSelectorBox);
            Controls.Add(_dbFilePathTextBox);
            _groupBox.Text = "";
            _groupBox.Location = new Point(5, 50);
            Form1_Resize(this, EventArgs.Empty);
            _groupBox.Controls.Add(_dataGridView);
            Controls.Add(_groupBox);
            _insertBtn.Click += (sender, e) => Execute(table => _tables[table].Insert(_sqliteConnection).ExecuteNonQuery(), sender, e);
            _updateBtn.Click += (sender, e) => { if (PrimaryKeyPopulatedCheck()) Execute(table => _tables[table].Update(_sqliteConnection).ExecuteNonQuery(), sender, e); };
            _deleteBtn.Click += (sender, e) => { if (PrimaryKeyPopulatedCheck()) Execute(table => _tables[table].Delete(_sqliteConnection).ExecuteNonQuery(), sender, e); };
            _dataGridView.SelectionChanged += _dataGridView_SelectionChanged;
            _dataGridView.Click += _dataGridView_SelectionChanged;
            _groupBox.Controls.Add(_insertBtn);
            _groupBox.Controls.Add(_updateBtn);
            _groupBox.Controls.Add(_deleteBtn);
            _groupBox.Visible = false;
            _dbFilePathTextBox.Click += _dbFilePathTextBox_Click;
            Resize += Form1_Resize;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            _groupBox.Size = new Size(Width - 33, Height - 105);
            _dataGridView.Size = new Size(_groupBox.Size.Width - 350, _groupBox.Height - 10);
        }

        private void _dbFilePathTextBox_Click(object sender, EventArgs e)
        {
            _tableSelectorBox.Items.Clear();
            _groupBox.Visible = false;
            var openFile = new OpenFileDialog();
            openFile.Title = "Load Sqlite db";
            if (openFile.ShowDialog() != DialogResult.OK) { return; }
            _tables = new Dictionary<string, TableInfo>();
            _dbFilePathTextBox.Text = openFile.FileName;
            _sqliteConnection = new SqliteConnection($"Data Source={openFile.FileName}");

            var tableNames = _sqliteConnection.Query<TableDetail>(@"SELECT tbl_name as TableName,Sql from sqlite_master").ToArray();
            foreach (var item in tableNames)
            {
                var response = _sqliteConnection.Query<ColInfo>($"PRAGMA table_info({item.TableName});").ToArray();

                foreach (var col in response) col.Sql = item.Sql;

                _tables.Add(item.TableName, new TableInfo { TableName = item.TableName, ColInfos = response });
            }
            _tableSelectorBox.Visible = true;
            _tableSelectorBox.Items.AddRange(tableNames.Select(w => w.TableName).ToArray());

        }

        private bool PrimaryKeyPopulatedCheck()
        {
            var valueIsNotNullOrEmpty = !string.IsNullOrWhiteSpace(_tables[_groupBox.Name].PrimaryKey.Value);
            if (!valueIsNotNullOrEmpty)
                MessageBox.Show("To update and delete you need a primary key to be populated");
            return valueIsNotNullOrEmpty;
        }

        private void _dataGridView_SelectionChanged(object sender, EventArgs e)
        {
            if (Loading) return;
            var dict = new Dictionary<string, string>();

            foreach (DataGridViewCell cell in _dataGridView.SelectedCells)
                dict.Add(_dataGridView.Columns[cell.ColumnIndex].HeaderText, cell.Value.ToString());

            _tables[_groupBox.Name].SetValues(dict);
        }

        private void Populate()
        {
            Loading = true;
            var dataTable = new DataTable();
            var colNames = _tables[_groupBox.Name].ColInfos.Select(w => w.Name).ToArray();
            dataTable.Columns.AddRange(colNames.Select(w => new DataColumn(w)).ToArray());

            _sqliteConnection.Open();
            using (var read = _tables[_groupBox.Name].GetAll(_sqliteConnection).ExecuteReader())
            {
                while (read.Read())
                    dataTable.Rows.Add(colNames.Select(item => read.GetValue(read.GetOrdinal(item))).ToArray());
            }

            _sqliteConnection.Close();
            _dataGridView.DataSource = dataTable;
            Loading = false;
        }

        private void Execute(Action<string> action, object sender, EventArgs e)
        {
            _sqliteConnection.Open();
            try
            {
                action(_groupBox.Name);
                MessageBox.Show("Done");
                foreach (var w in _tables[_groupBox.Name].ColInfos) w.SetValue("");
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }

            _sqliteConnection.Close();
            TableSelectorBox_SelectedValueChanged(sender, e);
        }

        private void TableSelectorBox_SelectedValueChanged(object sender, EventArgs e)
        {
            var selected = _tableSelectorBox.SelectedItem.ToString() ?? "";
            foreach (var item in _addedControls) _groupBox.Controls.Remove(item);

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

            _insertBtn.Top = top;
            _updateBtn.Top = top;
            _deleteBtn.Top = top;
            _groupBox.Visible = true;
            Populate();
        }
    }
}

public class TableInfo
{
    private List<Control> controls = null;
    public ColInfo PrimaryKey => ColInfos.FirstOrDefault(w => w.Pk);
    public string TableName { get; set; }
    public ColInfo[] ColInfos { get; set; }

    public SqliteCommand GetAll(SqliteConnection connection) => new SqliteCommand($"select * from {TableName}", connection);

    public SqliteCommand Insert(SqliteConnection connection)
    {
        var items = ColInfos.Where(q => q.Value != string.Empty).Where(e => !e.AutoGen).ToArray();
        var names = items.Select(w => w.Name).ToArray();
        var sql =
            $"insert into {TableName} ({string.Join(",", names)}) values ({string.Join(",", names.Select(w => $"@{w}"))})";
        var cmd = new SqliteCommand(sql, connection);
        foreach (var item in items)
            cmd.Parameters.Add(new SqliteParameter($"@{item.Name}", item.Value == "BLANK" ? "" : item.Value));

        return cmd;
    }

    public SqliteCommand Delete(SqliteConnection connection) => new($"delete from {TableName} where {PrimaryKey.Name} = {PrimaryKey.Value}", connection);

    public SqliteCommand Update(SqliteConnection connection)
    {
        var items = ColInfos.Select(w => w.Name).ToArray();
        var part = string.Join(",", items.Select(w => $"{w} = @{w}"));
        var cmd = new SqliteCommand($"update {TableName} set {part} where {PrimaryKey.Name} = {PrimaryKey.Value}", connection);
        foreach (var item in ColInfos)
            cmd.Parameters.Add(new SqliteParameter($"@{item.Name}", item.Value == "BLANK" ? "" : item.Value == "" ? null : item.Value));
        return cmd;
    }


    public void SetValues(Dictionary<string, string> dict)
    {
        foreach (var item in ColInfos) item.SetValue("");

        foreach (var item in dict)
        {
            var col = ColInfos.FirstOrDefault(w => w.Name == item.Key);
            if (col is null) continue;
            col.SetValue(item.Value.ToString());
        }
    }

    public List<Control> Controls()
    {
        controls ??= ColInfos.Select(w => w.WindowControl()).ToList();
        return controls;
    }
}

public class TableDetail
{
    public string TableName { get; set; }
    public string Sql { get; set; }
}

public record ColInfo
{
    public bool Pk { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Sql { get; set; }
    public bool AutoGen => Pk && Sql.ToLower().Contains("autoincrement");
    private Control ControlItem = new TextBox();

    public void SetValue(string text)
    {
        if (ControlItem is CheckBox box)
        {
            box.Checked = bool.Parse(text == "" ? "false" : text);
            return;
        }
        ControlItem.Text = text;
    }

    public string Value => ControlItem is CheckBox box ? box.Checked.ToString() : ControlItem.Text;

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
        panel.Controls.Add(ControlItem);
        ControlItem.Enabled = !AutoGen;

        return panel;
    }

    private void TextBox_TextChanged(object sender, EventArgs e)
    {
        if (!ControlItem.Enabled) { return; }
        switch (CalculateType())
        {
            case CSharpType.DATETIME:
            case CSharpType.DATE:
                ControlItem.BackColor = DateTime.TryParse(Value, out _) ? Color.White : Color.Red;
                break;
            case CSharpType.INTEGER:
                ControlItem.BackColor = int.TryParse(Value, out _) ? Color.White : Color.Red;
                break;
            case CSharpType.REAL:
                ControlItem.BackColor = decimal.TryParse(Value, out _) ? Color.White : Color.Red;
                break;
            case CSharpType.BOOl:
                ControlItem.BackColor = bool.TryParse(Value, out _) ? Color.White : Color.Red;
                break;
        }
    }

    private CSharpType? cSharpType = null;

    public CSharpType CalculateType()
    {
        if (cSharpType != null) return cSharpType.Value;

        var lower = Type.ToLower();

        if (lower.Contains("int"))
            cSharpType = CSharpType.INTEGER;
        else if (lower.Contains("decimal") || lower.Contains("numeric") || lower.Contains("real") ||
            lower.Contains("double") || lower.Contains("float"))
            cSharpType = CSharpType.REAL;
        else if (lower.Contains("datetime"))
            cSharpType = CSharpType.DATETIME;
        else if (lower.Contains("date"))
            cSharpType = CSharpType.DATE;
        else if (lower.Contains("bit") || lower.Contains("boolean"))
            cSharpType = CSharpType.BOOl;
        else
            cSharpType = CSharpType.TEXT;

        return cSharpType.Value;
    }
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
