using Hakufu.Services;

namespace Hakufu.MVVM.ViewModel;

public record ShortcutEntry(string Keys, string Description);
public record LegalSection(string Title, string Body);

public class HelpViewModel : BaseViewModel
{
    private readonly INavigationService _nav;

    public HelpViewModel(INavigationService nav) => _nav = nav;

    public IReadOnlyList<ShortcutEntry> Shortcuts { get; } =
    [
        new("← / →",          "Página anterior / siguiente"),
        new("Espacio",         "Siguiente página"),
        new("F / F11",         "Activar / salir modo zen"),
        new("ESC",             "Salir modo zen"),
        new("1",               "Modo una página"),
        new("2",               "Modo dos páginas"),
        new("Ctrl + W",        "Cerrar lector"),
    ];

    public RelayCommand GoBackCommand => new(() => _nav.NavigateTo<HomeViewModel>());

    // ── Términos, condiciones y aviso legal ─────────────────────────────────
    // Aviso: este texto es un modelo genérico redactado con fines
    // informativos y de protección razonable del desarrollador; no sustituye
    // el asesoramiento de un abogado. Se recomienda revisión profesional
    // antes de usarlo como base legal definitiva en caso de disputa real.
    public IReadOnlyList<LegalSection> LegalSections { get; } =
    [
        new("1. Objeto y aceptación de los términos",
            "Los presentes Términos y Condiciones de Uso, junto con el Aviso Legal y la Política de Privacidad " +
            "que se incluyen a continuación (en conjunto, los «Términos»), regulan el acceso, la instalación y el " +
            "uso de la aplicación de escritorio «Hakufu» (en adelante, la «Aplicación»), desarrollada y distribuida " +
            "por Daniel Poza Rivera (en adelante, el «Desarrollador»), con domicilio operativo en España y sujeto, " +
            "por tanto, a la legislación española y, en lo que resulte de aplicación, al Derecho de la Unión " +
            "Europea. La descarga, instalación, ejecución o uso de la Aplicación, en cualquiera de sus versiones, " +
            "implica la aceptación plena, expresa y sin reservas de la totalidad de estos Términos por parte del " +
            "usuario (en adelante, el «Usuario»). Si el Usuario no está de acuerdo con alguna de las cláusulas " +
            "aquí recogidas, deberá abstenerse de instalar, ejecutar o utilizar la Aplicación, y en su caso " +
            "desinstalarla de inmediato de cualquier dispositivo bajo su control. El Desarrollador se reserva el " +
            "derecho a modificar estos Términos en cualquier momento y sin previo aviso individualizado; la " +
            "versión vigente será siempre la publicada en el repositorio oficial de distribución de la Aplicación " +
            "y/o mostrada dentro de la propia Aplicación en la sección de Ayuda."),

        new("2. Naturaleza de la Aplicación y ausencia de contenidos propios",
            "Hakufu es, exclusivamente, una herramienta local de organización, catalogación y lectura de archivos " +
            "en formato PDF, CBR y CBZ que el propio Usuario posee, almacena y gestiona en sus propios " +
            "dispositivos y/o en su propia cuenta personal de servicios de terceros (por ejemplo, Dropbox, " +
            "cuando el Usuario decide vincularla voluntariamente). La Aplicación NO aloja, NO almacena en " +
            "servidores propios del Desarrollador, NO indexa, NO cataloga, NO distribuye, NO comparte, NO " +
            "publica, NO enlaza ni NO facilita de ninguna otra forma el acceso a obras protegidas por derechos de " +
            "propiedad intelectual, ni ofrece buscador, directorio, enlace ni mecanismo alguno de descarga de " +
            "contenidos de terceros. Todo archivo que aparezca en la biblioteca de un Usuario ha sido cargado, " +
            "importado o vinculado única y exclusivamente por decisión, acción e iniciativa propias de dicho " +
            "Usuario, sin intervención, selección, curación, recomendación ni conocimiento previo por parte del " +
            "Desarrollador acerca del contenido, origen o licitud de dichos archivos."),

        new("3. Responsabilidad exclusiva del Usuario sobre los contenidos y prohibición de uso ilícito",
            "El Usuario declara y garantiza, bajo su exclusiva responsabilidad, que es titular de los derechos de " +
            "propiedad intelectual sobre cualquier archivo que introduzca en la Aplicación, o que cuenta con la " +
            "licencia, autorización o cesión de derechos suficiente y válida —otorgada por el titular legítimo de " +
            "dichos derechos— para reproducir, almacenar y visualizar dicho contenido en un entorno privado, de " +
            "conformidad con el Texto Refundido de la Ley de Propiedad Intelectual, aprobado por el Real Decreto " +
            "Legislativo 1/1996, de 12 de abril (LPI), y demás normativa concordante, incluida la Directiva (UE) " +
            "2019/790 sobre derechos de autor y derechos afines en el mercado único digital. Queda expresa, " +
            "terminante y absolutamente prohibido utilizar la Aplicación para reproducir, almacenar, visualizar, " +
            "catalogar o gestionar de cualquier forma copias no autorizadas, piratas, ilícitas o que infrinjan " +
            "derechos de propiedad intelectual o industrial de terceros, así como emplear la Aplicación como " +
            "instrumento, medio o soporte para la comisión de los delitos contra la propiedad intelectual " +
            "tipificados en los artículos 270 a 272 del Código Penal español (Ley Orgánica 10/1995, de 23 de " +
            "noviembre, y sus sucesivas reformas), ni para facilitar, promover o participar en actos de piratería " +
            "digital de cualquier naturaleza. El Desarrollador no ejerce ni puede ejercer control, supervisión ni " +
            "verificación previa alguna sobre los archivos que cada Usuario decide cargar en su instalación " +
            "local, dado el carácter privado, local y descentralizado de dicho tratamiento, por lo que no puede " +
            "ser considerado responsable, cómplice, cooperador necesario ni encubridor de los usos ilícitos que " +
            "terceros Usuarios pudieran hacer de la Aplicación."),

        new("4. Exención y limitación de responsabilidad del Desarrollador",
            "En la máxima medida permitida por la legislación aplicable, el Desarrollador queda exonerado de " +
            "cualquier responsabilidad civil, penal o administrativa derivada del uso indebido, ilícito o " +
            "contrario a estos Términos que el Usuario o cualquier tercero pudiera hacer de la Aplicación, " +
            "incluyendo —sin ánimo exhaustivo— la carga, almacenamiento, reproducción, distribución o puesta a " +
            "disposición de contenidos protegidos por derechos de autor sin la debida autorización. El Usuario " +
            "asume, por sí mismo y frente a cualesquiera reclamaciones de terceros (incluidas entidades de " +
            "gestión de derechos, titulares de obras, autoridades administrativas o judiciales), toda la " +
            "responsabilidad que pudiera derivarse del contenido que decida gestionar a través de la Aplicación, " +
            "manteniendo indemne al Desarrollador frente a cualquier reclamación, sanción, multa, indemnización, " +
            "coste procesal o perjuicio de cualquier índole que pudiera derivarse, directa o indirectamente, del " +
            "incumplimiento por parte del Usuario de la normativa de propiedad intelectual o de estos Términos. " +
            "La Aplicación se ofrece «tal cual» («as is») y «según disponibilidad» («as available»), sin " +
            "garantías de ningún tipo, ni expresas ni implícitas, incluyendo, sin limitación, garantías de " +
            "comerciabilidad, idoneidad para un fin concreto, ausencia de errores o disponibilidad ininterrumpida. " +
            "El Desarrollador no garantiza que la Aplicación esté libre de errores, ni que su funcionamiento sea " +
            "ininterrumpido, ni se responsabiliza de la pérdida de datos, archivos, progreso de lectura o " +
            "configuraciones almacenadas localmente o en servicios de terceros vinculados por el Usuario."),

        new("5. Uso de Inteligencia Artificial",
            "El Usuario queda informado, a los efectos de transparencia exigidos por el Reglamento (UE) 2024/1689 " +
            "del Parlamento Europeo y del Consejo, de 13 de junio de 2024, por el que se establecen normas " +
            "armonizadas en materia de inteligencia artificial (Reglamento de Inteligencia Artificial o «AI Act»), " +
            "así como por cualquier otra normativa española o europea presente o futura en la materia, de que la " +
            "Aplicación ha sido desarrollada con el apoyo de herramientas de inteligencia artificial generativa " +
            "y/o puede incorporar, en su código, funcionalidades, procesos de desarrollo, mantenimiento o " +
            "componentes auxiliares, sistemas o modelos de inteligencia artificial (en adelante, «Sistemas de " +
            "IA»). Los Sistemas de IA eventualmente empleados no adoptan decisiones automatizadas con efectos " +
            "jurídicos o de importancia significativa sobre el Usuario, no realizan perfilado con fines " +
            "publicitarios, no procesan categorías especiales de datos personales, ni se emplean con fines de " +
            "identificación biométrica, puntuación social, manipulación subliminal o explotación de " +
            "vulnerabilidades de colectivo alguno, quedando por tanto fuera de las categorías de «riesgo " +
            "inaceptable» o «alto riesgo» definidas por el AI Act. El Desarrollador no garantiza la ausencia " +
            "absoluta de errores, imprecisiones o comportamientos inesperados derivados, directa o " +
            "indirectamente, del empleo de dichos Sistemas de IA en el proceso de desarrollo de la Aplicación, y " +
            "declina cualquier responsabilidad por daños derivados de tales circunstancias, sin perjuicio de su " +
            "compromiso de corregir, dentro de lo razonable y en la medida de sus posibilidades como proyecto de " +
            "software independiente, los defectos que le sean puestos en conocimiento."),

        new("6. Cuentas de usuario, sincronización con servicios de terceros y protección de datos",
            "La Aplicación permite, de forma opcional y bajo la exclusiva voluntad del Usuario, la creación de " +
            "una cuenta personal y la vinculación de servicios de terceros —incluyendo, entre otros, Dropbox— " +
            "con el fin de sincronizar o respaldar la biblioteca del Usuario. Dicha vinculación se realiza " +
            "íntegramente mediante los mecanismos oficiales de autenticación del tercero correspondiente (OAuth), " +
            "sin que el Desarrollador llegue a conocer, almacenar ni tener acceso a las credenciales de acceso " +
            "del Usuario a dichos servicios. El tratamiento de los datos personales facilitados por el Usuario se " +
            "realiza de conformidad con el Reglamento (UE) 2016/679, General de Protección de Datos (RGPD), y con " +
            "la Ley Orgánica 3/2018, de 5 de diciembre, de Protección de Datos Personales y garantía de los " +
            "derechos digitales (LOPDGDD). El Usuario podrá ejercer en cualquier momento sus derechos de acceso, " +
            "rectificación, supresión, oposición, limitación del tratamiento y portabilidad dirigiéndose al " +
            "Desarrollador a través de los canales de contacto habilitados en la propia Aplicación o en su " +
            "repositorio oficial. Asimismo, resulta de aplicación, en lo pertinente, la Ley 34/2002, de 11 de " +
            "julio, de Servicios de la Sociedad de la Información y de Comercio Electrónico (LSSICE)."),

        new("7. Limitación de responsabilidad por daños",
            "En ningún caso el Desarrollador será responsable frente al Usuario ni frente a terceros de daños " +
            "indirectos, incidentales, especiales, punitivos o consecuentes, incluyendo, sin limitación, lucro " +
            "cesante, pérdida de datos, pérdida de oportunidad de negocio, interrupción de actividad o daños " +
            "reputacionales, derivados del uso o de la imposibilidad de uso de la Aplicación, aun cuando el " +
            "Desarrollador hubiera sido advertido de la posibilidad de tales daños. En todo caso, y en la máxima " +
            "medida permitida por la ley aplicable, la responsabilidad total del Desarrollador frente al Usuario, " +
            "por cualquier concepto derivado del uso de la Aplicación, quedará limitada al importe efectivamente " +
            "abonado por el Usuario por el uso de la Aplicación, que, siendo esta de distribución gratuita, " +
            "asciende a cero (0) euros."),

        new("8. Indemnidad",
            "El Usuario se compromete a mantener indemne y a defender al Desarrollador frente a cualquier " +
            "reclamación, demanda, acción legal, sanción administrativa, requerimiento o procedimiento —judicial " +
            "o extrajudicial— iniciado por cualquier tercero (incluidas entidades de gestión de derechos de " +
            "autor, titulares de obras, autoridades públicas o particulares) como consecuencia del uso que el " +
            "propio Usuario haga de la Aplicación en contravención de estos Términos o de la normativa vigente, " +
            "incluyendo los honorarios razonables de abogados y demás costes procesales en que pudiera incurrir " +
            "el Desarrollador para su defensa."),

        new("9. Menores de edad",
            "La Aplicación no está dirigida específicamente a menores de edad. Si un menor accede o utiliza la " +
            "Aplicación, se presume que cuenta con la autorización de sus padres, tutores o representantes " +
            "legales, quienes serán responsables de supervisar dicho uso y de la naturaleza de los contenidos que " +
            "el menor decida gestionar a través de ella, de conformidad con el artículo 84 de la LOPDGDD relativo " +
            "a la protección de los derechos digitales de los menores."),

        new("10. Propiedad intelectual e industrial de la propia Aplicación",
            "El código fuente, el diseño, la marca «Hakufu», los logotipos, iconos y demás elementos distintivos " +
            "de la Aplicación son titularidad del Desarrollador o se utilizan bajo licencia, y están protegidos " +
            "por la normativa española y comunitaria de propiedad intelectual e industrial. Queda prohibida su " +
            "reproducción, modificación, distribución o comunicación pública total o parcial sin autorización " +
            "expresa del Desarrollador, salvo en los términos, en su caso, de la licencia de código abierto bajo " +
            "la que se publique el repositorio correspondiente, si procede."),

        new("11. Modificaciones de la Aplicación y de los presentes Términos",
            "El Desarrollador se reserva el derecho a modificar, actualizar, suspender o discontinuar, total o " +
            "parcialmente y sin previo aviso, la Aplicación, sus funcionalidades o los presentes Términos. El uso " +
            "continuado de la Aplicación tras la publicación de una nueva versión de estos Términos implicará la " +
            "aceptación tácita de dichas modificaciones por parte del Usuario."),

        new("12. Legislación aplicable y jurisdicción",
            "Los presentes Términos se rigen en su totalidad por la legislación española. Para la resolución de " +
            "cualquier controversia que pudiera derivarse del acceso o uso de la Aplicación, y sin perjuicio de " +
            "los fueros imperativos que pudieran corresponder al Usuario en su condición de consumidor conforme " +
            "al Real Decreto Legislativo 1/2007, de 16 de noviembre, por el que se aprueba el Texto Refundido de " +
            "la Ley General para la Defensa de los Consumidores y Usuarios, las partes se someten a los Juzgados " +
            "y Tribunales españoles competentes según la normativa procesal aplicable, renunciando expresamente a " +
            "cualquier otro fuero que pudiera corresponderles cuando la ley lo permita."),

        new("13. Contacto",
            "Para cualquier cuestión relacionada con estos Términos, ejercicio de derechos en materia de " +
            "protección de datos, notificación de contenidos presuntamente infractores o cualquier otra " +
            "consulta, el Usuario puede dirigirse al Desarrollador a través de los canales de contacto indicados " +
            "en el repositorio oficial de la Aplicación."),
    ];
}
