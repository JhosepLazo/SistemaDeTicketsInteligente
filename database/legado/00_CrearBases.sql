Set NoCount On
Set Xact_Abort On
Go

Use [master]
Go

If Db_Id(N'GestionSistemas') Is Null Create Database [GestionSistemas]
Go
If Db_Id(N'IntranetCalimod') Is Null Create Database [IntranetCalimod]
Go
If Db_Id(N'Spring') Is Null Create Database [Spring]
Go

Alter Database [GestionSistemas] Set Compatibility_Level = 160
Alter Database [IntranetCalimod] Set Compatibility_Level = 160
Alter Database [Spring] Set Compatibility_Level = 160

-- El entorno local tiene memoria limitada; Query Store no es necesario para ejecutar la aplicación.
Alter Database [GestionSistemas] Set Query_Store = Off
Alter Database [IntranetCalimod] Set Query_Store = Off
Alter Database [Spring] Set Query_Store = Off
Go
