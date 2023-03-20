using Microsoft.Data.Sqlite;

namespace WinFormsApp2
{
    public partial class Form1 : Form
    {
        private ComboBox TableSelectorBox = new ComboBox();
        static SqliteConnection sqliteConnection = null;
        Mkb.DapperRepo.Repo.SqlRepo Repo = new Mkb.DapperRepo.Repo.SqlRepo(() => sqliteConnection);
        private Dictionary<string, TableInfo> tables = new Dictionary<string, TableInfo>();
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var openFile = new OpenFileDialog();
            if (openFile.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            tables = new Dictionary<string, TableInfo>();

            sqliteConnection = new SqliteConnection($"Data Source={openFile.FileName}");

            var tableNames = Repo.QueryMany<string>("SELECT tbl_name from sqlite_master").ToArray();
            foreach (var item in tableNames)
            {
                var response = Repo.QueryMany<ColInfo>($"PRAGMA table_info({item});");

                tables.Add(item, new TableInfo { TableName = item, ColInfos = response.ToArray() });
            }

            TableSelectorBox.Items.AddRange(tableNames);
            this.components.Add(TableSelectorBox);
        }
    }
}

public class TableInfo
{
    public string TableName { get; set; }
    public ColInfo[] ColInfos { get; set; }
}

public record ColInfo
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool NotNull { get; set; }

    public CSharpType RealType => CalculateType();

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