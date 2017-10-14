using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.Data.Odbc;
using System.Data.OleDb;

using System.Data.SqlClient;
using System.IO;

using LFSO81Lib;
using DocumentProcessor81;
using PdfExporter81;

namespace LaserficheCGA
{
    public partial class formPrincipal : Form
    {
        private LFConnection conn;
        private LFDatabase db;                
        private LFServer server;

        private string foxpro_db;
        private string sqlserver_db;
        private string sqlserver_sp;

        public formPrincipal() 
        {
            InitializeComponent();
        }

        private void formPrincipal_Load(object sender, EventArgs e)
        {
            this.conn = new LFConnection();
            this.server = new LFApplication().GetServerByName("server_lf");
            this.db = server.GetDatabaseByName("DA-HINOJOSA");
            this.conn.UserName = "pablo.montalvo";
            this.conn.Password = "P123456";
            this.conn.Create(this.db);

            this.cargarCombos();
            this.cmbOficinas.SelectedIndex = 0;
        }
        
        private void cargarCombos()
        {
            this.cmbOficinas.Items.Add("AGENCIA ADUANAL");
            this.cmbOficinas.Items.Add("AGENCIA ADUANAL - ARCHIVOS S.F."); // Archivos sin folio fiscal
            this.cmbOficinas.Items.Add("FUMIT");
            this.cmbOficinas.Items.Add("TERMINAL");
            this.cmbOficinas.Items.Add("INTERFLET");
        }   

        private void cmbOficinas_SelectedIndexChanged(object sender, EventArgs e)
        {
            int seleccion = cmbOficinas.SelectedIndex;
            switch (seleccion)
            {
                case 0:
                    this.foxpro_db = "datrefer";
                    this.sqlserver_db = "1G_DAH_AA";
                    this.sqlserver_sp = "UP_Consulta_ArchivosAgencia";
                    break;
                case 1:
                    this.foxpro_db = "datrefer";
                    this.sqlserver_db = "1G_DAH_AA";
                    this.sqlserver_sp = "UP_Consulta_ArchivosAgencia_SF";
                    break;
                case 2:
                    this.foxpro_db = "fumit";
                    this.sqlserver_db = "1G_DAH_FUMIT";
                    this.sqlserver_sp = "UP_Consulta_ArchivosFumit";
                    break;
                case 3:
                    this.foxpro_db = "terminal";
                    this.sqlserver_db = "1G_DAH_TERMINAL";
                    this.sqlserver_sp = "UP_Consulta_ArchivosTerminal";
                    break;
                case 4:
                    this.foxpro_db = "interflet";
                    this.sqlserver_db = "PENDIENTE";
                    this.sqlserver_sp = "PENDIENTE";
                    break;
            }
        }

        private void btnBuscar_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            string fechaInicial = this.cmbFechaInicial.Value.ToString("MM/dd/yyyy");
            string fechaFinal = this.cmbFechaFinal.Value.ToString("MM/dd/yyyy");
            try
            {
                this.gridResultado.DataSource = this.getData(fechaInicial, fechaFinal).Tables[0];               
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }
        }

        private DataSet getData(string fechaInicial, string fechaFinal)
        {
            using (SqlConnection conn = this.getConnectionSQL())
            {
                using (SqlCommand command = new SqlCommand(this.sqlserver_sp, conn))
                { 
                    command.CommandType = CommandType.StoredProcedure;
                    conn.Open();
                    
                    command.Parameters.AddWithValue("@FechaIni", fechaInicial);
                    command.Parameters.AddWithValue("@FechaFin", fechaFinal);

                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        DataSet data = new DataSet();
                        adapter.Fill(data);
                        return data;
                    }
                }
            }
        }

        private SqlConnection getConnectionSQL()
        {
            return new SqlConnection("Integrated Security=False;User ID=sa;Password=sa;Initial Catalog="+this.sqlserver_db+";Data Source=175.50.5.3");
        }  

        private void btnProcesar_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            if (this.gridResultado.Rows.Count > 0)
            {
                this.deleteFoxProData();
               
                foreach (DataGridViewRow row in this.gridResultado.Rows)
                {
                    string archivo, cliente, referencia, movimiento, numero, serie, anio, mes, ruta, folio;
                    archivo = row.Cells[0].Value.ToString();
                    cliente = row.Cells[1].Value.ToString();
                    referencia = row.Cells[2].Value.ToString();
                    movimiento = row.Cells[3].Value.ToString();
                    numero = row.Cells[4].Value.ToString();
                    serie = row.Cells[5].Value.ToString();
                    anio = row.Cells[6].Value.ToString();
                    mes = row.Cells[7].Value.ToString();
                    ruta = getRuta(referencia);
                    folio = "6000";

                    this.insertFoxProData(archivo, cliente, referencia, ruta, movimiento, numero, serie, folio); 
                   
                   // this.moveFile(archivo);                    
                }                
            }
            MessageBox.Show("Proceso terminado");
            Cursor = Cursors.Arrow;
        }

        private void deleteFoxProData()
        {
            this.actionFoxPro("DELETE FROM " + this.foxpro_db);
        }

        private string getRuta(string referencia)
        {

            string ruta = "SIN RUTA EN LF";
            try
            {
                LFSearch busqueda = this.db.CreateSearch();
                string queryLF = "{LF:ParentName=\"" + referencia + "\"}";
                busqueda.Command = queryLF;
                busqueda.BeginSearch(true);
                ILFCollection archivos = busqueda.GetSearchHits();
                if (archivos.Count > 0)
                {
                    LFSearchHit hit = (LFSearchHit)archivos[1];
                    ILFEntry entry = (ILFEntry)hit.Entry;
                    LFFolder folder = entry.ParentFolder;
                    ruta = folder.FullPath + @"\";                    
                }
                busqueda.Dispose();
                return ruta;
            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return ruta;
            }
        }

        private void insertFoxProData(string archivo, string cliente, string referencia, string ruta, string movimiento, string numero, string serie, string folio)
        {
            this.actionFoxPro("INSERT INTO " + this.foxpro_db + " (archivo, Cliente, referencia, Ruta, tipomov, numero, serie, folio) VALUES ('" + archivo + "'," + cliente + ",'" + referencia + "','" + ruta + "',1," + numero + ",'" + serie + "','" + serie + "' )");
        }

        private void actionFoxPro(string action)
        {
            string path = @"h:\sica\util\scontrol\";

            if (Directory.Exists(path))
            {
                using (OdbcConnection con = new OdbcConnection("Driver={Microsoft Visual FoxPro Driver};SourceType=DBF;SourceDB=" + path))
                {
                    con.Open();
                    using (OdbcCommand command = new OdbcCommand(action, con))
                    {
                        command.ExecuteNonQuery();                        
                    }
                }
            }
            else
            {
                MessageBox.Show("¡LA UBICACIÓN: " + path + " NO EXISTE!");
            }
        }

        private void btnCancelar_Click(object sender, EventArgs e)
        {
            conn.Terminate();
            Application.Exit();
        }

        private void moveFile(string archivo)
        {
            if (File.Exists(@"Z:\OneGoalCFD\XML_AA\" + archivo + ".pdf") && File.Exists(@"Z:\OneGoalCFD\XML_AA\" + archivo + ".xml"))
            {
                if (Directory.Exists(@"C:\Andres\BORRAR\XML_AA\"))
                {
                    File.Copy(@"Z:\OneGoalCFD\XML_AA\" + archivo + ".pdf", @"C:\Andres\BORRAR\XML_AA\" + archivo + ".pdf");
                    File.Copy(@"Z:\OneGoalCFD\XML_AA\" + archivo + ".xml", @"C:\Andres\BORRAR\XML_AA\" + archivo + ".xml");
                }
            }
        }
    }
}