using CRM.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace PWA.Servicios
{
    public class UserSessionState
    {
        public UsuarioDto? LoggedInUser { get; private set; }

        // Almacenes en memoria para datos del usuario
        public List<BarcosDto>? MisBarcos { get; private set; }
        public List<TramiteDto>? MisTramites { get; private set; }

        // Flags para saber si ya se han consultado alguna vez
        public bool MisBarcosCargados => MisBarcos != null;

        public void SetUser(UsuarioDto user) => LoggedInUser = user;

        public void GuardarBarcos(List<BarcosDto> barcos) => MisBarcos = barcos;

        public void ClearSession()
        {
            LoggedInUser = null;
            MisBarcos = null;
            MisTramites = null;
        }
    }
}
