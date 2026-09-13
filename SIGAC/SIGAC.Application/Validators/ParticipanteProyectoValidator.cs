using SIGAC.Application.DTOs.Proyectos;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Datos de un participante ya validados y normalizados, coherentes con la
    // exclusión mutua que respalda CK_ParticipantesProyecto_Discriminador: cuando
    // EsBeneficiario es true, BeneficiarioId viene lleno y los campos externos en
    // null; cuando es false, al revés.
    public sealed record ParticipanteProyectoValidado(
        bool EsBeneficiario,
        int? BeneficiarioId,
        string? NombreExterno,
        string? ContactoExterno);

    // Valida y normaliza un participante antes de guardarlo. La existencia del
    // beneficiario (y que esté activo) no se comprueba acá: ese chequeo necesita
    // consultar Beneficiarios, así que vive en ProyectosService, que ya depende de
    // IBeneficiariosRepository para eso.
    public static class ParticipanteProyectoValidator
    {
        public const int LongitudMaximaNombreExterno = 150;
        public const int LongitudMaximaContactoExterno = 150;

        public static ParticipanteProyectoValidado Validar(ParticipanteCrearDto dto)
        {
            if (dto.EsBeneficiario)
            {
                if (!dto.BeneficiarioId.HasValue)
                    throw new ValidationException("Debe indicar el beneficiario que participa.");

                return new ParticipanteProyectoValidado(true, dto.BeneficiarioId.Value, null, null);
            }

            var nombreExterno = TextoNormalizador.CompactarEspacios(dto.NombreExterno);

            if (nombreExterno.Length == 0)
                throw new ValidationException("El nombre del participante externo es obligatorio.");

            if (nombreExterno.Length > LongitudMaximaNombreExterno)
                throw new ValidationException(
                    $"El nombre no puede superar los {LongitudMaximaNombreExterno} caracteres.");

            // Opcional: un participante externo puede no dejar datos de contacto.
            // Vacío se guarda como NULL, mismo tratamiento que Donante.Telefono.
            var contactoExterno = TextoNormalizador.CompactarEspacios(dto.ContactoExterno);

            if (contactoExterno.Length > LongitudMaximaContactoExterno)
                throw new ValidationException(
                    $"El contacto no puede superar los {LongitudMaximaContactoExterno} caracteres.");

            return new ParticipanteProyectoValidado(
                false,
                null,
                nombreExterno,
                contactoExterno.Length == 0 ? null : contactoExterno);
        }
    }
}
