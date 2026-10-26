import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  ResourceRef,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, from, map, of, switchMap } from 'rxjs';

import { Api } from '../../../api/api';
import { ApiConfiguration } from '../../../api/api-configuration';
import { buscarSerieActivo } from '../../../api/fn/activos/buscar-serie-activo';
import { crearActivo } from '../../../api/fn/activos/crear-activo';
import { editarActivo } from '../../../api/fn/activos/editar-activo';
import { obtenerFichaActivo } from '../../../api/fn/activos/obtener-ficha-activo';
import { obtenerListasActivo } from '../../../api/fn/activos/obtener-listas-activo';
import { listarFotosActivo } from '../../../api/fn/fotos-de-activos/listar-fotos-activo';
import { listarElementosCatalogo } from '../../../api/fn/catalogos/listar-elementos-catalogo';
import { ActivoConSerieDto } from '../../../api/models/activo-con-serie-dto';
import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { Permisos } from '../../../core/auth/permisos';
import { ErrorApi, aErrorApi } from '../../../core/http/error-api';
import { SesionStore } from '../../../core/auth/sesion.store';
import { SedeSeleccionadaStore } from '../../../core/layout/sede-seleccionada.store';
import { NotificacionesService } from '../../../core/notificaciones.service';
import { AvisoComponent } from '../../../shared/ui/aviso.component';
import { confirmar } from '../../../shared/ui/confirmacion.dialog';
import { ConCambiosSinGuardar } from '../../../shared/util/cambios-sin-guardar';
import { SelectorCatalogoComponent } from '../../catalogos/components/selector-catalogo.component';
import { BuscadorEstablecimientoComponent, EstablecimientoElegido } from '../components/buscador-establecimiento.component';
import { FotosFormularioComponent } from '../components/fotos-formulario.component';
import { EdicionFotos } from '../data/edicion-fotos';
import {
  CapacidadDisco,
  Ficha,
  ValorDisco,
  ValorFormularioActivo,
  ValorModuloRam,
  fichaDesdeFormulario,
  formularioDesdeFicha,
  hayDatosQueSeDescartan,
  hoyIso,
  opcionesDeCatalogo,
  totalDiscosGb,
  totalRamGb,
} from '../data/formulario-activo';
import { aNumero, fechaRenovacion, formatoFecha, formatoGb, textoFicha } from '../data/textos-activo';
import { crearSugeridor } from '../data/sugerencias';
import { decimales, entero, ipValida, macValida, noAnteriorA, noPosteriorA } from '../data/validadores-activo';

const MAXIMO_DISCOS = 16;
const MAXIMO_MODULOS = 32;
const ESPERA_SERIE_MS = 500;

/** Campos técnicos que también lleva la ficha "Red". */
const CAMPOS_DE_RED = ['nombreEquipo', 'direccionIp', 'direccionMac'] as const;

/**
 * Registrar y editar un activo (docs/ARQUITECTURA.md §5.10). Las secciones técnicas dependen de la ficha del tipo
 * elegido (D43); las que no corresponden se ocultan y no se envían. Avisa si el número de serie ya existe en la red
 * y pregunta antes de salir con cambios sin guardar. Las fotos (D38) se suben después de guardar la ficha.
 */
@Component({
  selector: 'app-formulario-activo',
  imports: [
    NgTemplateOutlet,
    ReactiveFormsModule,
    RouterLink,
    MatAutocompleteModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    AvisoComponent,
    BuscadorEstablecimientoComponent,
    FotosFormularioComponent,
    SelectorCatalogoComponent,
  ],
  templateUrl: './formulario-activo.page.html',
  styleUrl: './formulario-activo.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(window:beforeunload)': 'alCerrarLaPestana($event)' },
})
export class FormularioActivoPage implements ConCambiosSinGuardar {
  private readonly api = inject(Api);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly router = inject(Router);
  private readonly dialogos = inject(MatDialog);
  private readonly notificaciones = inject(NotificacionesService);
  private readonly sesion = inject(SesionStore);
  private readonly sedeSeleccionada = inject(SedeSeleccionadaStore);
  private readonly elemento = inject<ElementRef<HTMLElement>>(ElementRef);

  /** Viene de la ruta (:id) al editar; no existe al registrar. */
  readonly id = input<string>();

  protected readonly esEdicion = computed(() => !!this.id());
  /**
   * El activo que se guarda: el de la ruta, o el recién registrado si alguna foto no se subió y el usuario reintenta
   * (así no se registra dos veces).
   */
  private readonly idCreado = signal<string | null>(null);
  private readonly idActivo = computed(() => this.id() ?? this.idCreado());
  protected readonly hoy = hoyIso();
  protected readonly maximoDiscos = MAXIMO_DISCOS;
  protected readonly maximoModulos = MAXIMO_MODULOS;
  protected readonly formatoGb = formatoGb;

  /** Agregar al catálogo desde el formulario (D45): los generales, el superadministrador; las áreas, también el administrador de la red. */
  protected readonly puedeAgregarGenerales = this.sesion.tienePermiso(Permisos.Catalogos.GestionarGlobales);
  protected readonly puedeAgregarAreas = this.sesion.tienePermiso(Permisos.Catalogos.GestionarSede);
  protected readonly textoFicha = textoFicha;

  /** Las fotos se gestionan con Activos.Editar (D47). */
  protected readonly puedeFotos = this.sesion.tienePermiso(Permisos.Activos.Editar);
  protected readonly fotos = new EdicionFotos(this.api, inject(ApiConfiguration).rootUrl);
  protected readonly errorFotos = signal<string | null>(null);

  // ---------- Formulario ----------

  protected readonly formulario = this.fb.group({
    generales: this.fb.group({
      codigoPatrimonial: ['', [Validators.required, Validators.maxLength(30)]],
      tipoActivoId: new FormControl<string | null>(null, Validators.required),
      marcaId: new FormControl<string | null>(null, Validators.required),
      modelo: ['', [Validators.required, Validators.maxLength(100)]],
      numeroSerie: ['', Validators.maxLength(60)],
      descripcion: ['', Validators.maxLength(250)],
      areaId: new FormControl<string | null>(null, Validators.required),
      ubicacionFisica: ['', Validators.maxLength(150)],
      responsable: ['', Validators.maxLength(150)],
      estadoActivoId: new FormControl<string | null>(null, Validators.required),
      conservacion: new FormControl<string | null>('Bueno', Validators.required),
      criticidad: new FormControl<string | null>(null),
      observaciones: ['', Validators.maxLength(1000)],
    }),
    adquisicion: this.fb.group({
      fechaAdquisicion: ['', noPosteriorA(() => hoyIso())],
      proveedor: ['', Validators.maxLength(150)],
      numeroOrdenCompra: ['', Validators.maxLength(50)],
      tipoAdquisicion: new FormControl<string | null>(null),
      valorAdquisicion: new FormControl<number | null>(null, [Validators.min(0), decimales(2)]),
      vidaUtilAnios: new FormControl<number | null>(null, [Validators.min(1), Validators.max(50), entero]),
      finGarantia: ['', noAnteriorA('fechaAdquisicion')],
    }),
    tecnicos: this.fb.group({
      procesadorId: new FormControl<string | null>(null),
      generacionProcesador: new FormControl<string | null>(null),
      nucleos: new FormControl<number | null>(null, [Validators.min(1), Validators.max(512), entero]),
      tipoGrafica: new FormControl<string | null>(null),
      modeloGrafica: ['', Validators.maxLength(100)],
      pantalla: new FormControl<number | 'otra' | null>(null),
      pantallaOtra: new FormControl<number | null>(null),
      sistemaOperativoId: new FormControl<string | null>(null),
      versionSistemaOperativo: ['', Validators.maxLength(50)],
      nombreEquipo: ['', [Validators.maxLength(63), Validators.pattern(/^[A-Za-z0-9][A-Za-z0-9.-]*$/)]],
      dominioGrupo: ['', Validators.maxLength(100)],
      direccionIp: ['', ipValida],
      direccionMac: ['', macValida],
      antivirusId: new FormControl<string | null>(null),
      discoCifrado: new FormControl<boolean | null>(null),
      puertos: new FormControl<string[]>([], { nonNullable: true }),
      accesorios: new FormControl<string[]>([], { nonNullable: true }),
    }),
    discos: this.fb.array<FormGroup>([]),
    modulosRam: this.fb.array<FormGroup>([]),
  });

  protected readonly generales = this.formulario.controls.generales.controls;
  protected readonly adquisicion = this.formulario.controls.adquisicion.controls;
  protected readonly tecnicos = this.formulario.controls.tecnicos.controls;
  protected readonly discos = this.formulario.controls.discos as FormArray<FormGroup>;
  protected readonly modulosRam = this.formulario.controls.modulosRam as FormArray<FormGroup>;

  /** Valor completo (incluidos los campos ocultos), para los cálculos de la pantalla. */
  protected readonly valor = toSignal(this.formulario.valueChanges.pipe(map(() => this.valorActual())), {
    initialValue: this.valorActual(),
  });

  protected readonly establecimiento = signal<EstablecimientoElegido | null>(null);
  protected readonly intentoGuardar = signal(false);
  protected readonly guardando = signal(false);
  protected readonly errorGuardar = signal<ErrorApi | null>(null);
  /** Ya se guardó: salir no pide confirmación. */
  private guardado = false;
  /** La ficha a editar ya se volcó en el formulario. */
  private readonly cargada = signal(false);
  /** Las fotos guardadas ya se volcaron en el formulario. */
  private readonly fotosCargadas = signal(false);

  // ---------- Datos de la API ----------

  protected readonly listas = resource({ loader: () => this.api.invoke(obtenerListasActivo) });

  private readonly fichaActual = resource({
    params: () => (this.id() ? { id: this.id()! } : undefined),
    loader: ({ params }) => this.api.invoke(obtenerFichaActivo, params),
  });

  private readonly fotosActuales = resource({
    params: () => (this.id() && this.puedeFotos ? { id: this.id()! } : undefined),
    loader: ({ params }) => this.api.invoke(listarFotosActivo, params),
  });

  protected readonly tipos = this.catalogo('tipos-activo');
  protected readonly marcas = this.catalogo('marcas');
  protected readonly estados = this.catalogo('estados-activo');
  protected readonly sistemas = this.catalogo('sistemas-operativos');
  protected readonly procesadores = this.catalogo('procesadores');
  protected readonly antivirus = this.catalogo('antivirus');

  // Sugerencias de lo ya registrado en la red (D45).
  protected readonly sModelo = crearSugeridor('Modelo', () => ({ marcaId: this.generales.marcaId.value ?? undefined }));
  protected readonly sUbicacion = crearSugeridor('UbicacionFisica', () => ({ establecimientoId: this.establecimiento()?.id }));
  protected readonly sProveedor = crearSugeridor('Proveedor');
  protected readonly sVersion = crearSugeridor('VersionSistemaOperativo', () => ({
    sistemaOperativoId: this.tecnicos.sistemaOperativoId.value ?? undefined,
  }));
  protected readonly sDominio = crearSugeridor('DominioGrupo');
  protected readonly sDisco = crearSugeridor('MarcaModeloDisco');
  protected readonly sRam = crearSugeridor('MarcaModeloRam');

  /** Áreas de la red del establecimiento elegido. */
  protected readonly areas = resource({
    params: () => {
      const e = this.establecimiento();
      return e ? { sedeId: e.sedeId } : undefined;
    },
    loader: ({ params }) =>
      this.api.invoke(listarElementosCatalogo, { tipo: 'areas', sedeId: params.sedeId, incluirInactivos: true }),
  });

  protected readonly opcionesTipo = computed(() => this.opciones(this.tipos.value(), this.valor().generales.tipoActivoId));
  protected readonly opcionesMarca = computed(() => this.opciones(this.marcas.value(), this.valor().generales.marcaId));
  protected readonly opcionesEstado = computed(() => this.opciones(this.estados.value(), this.valor().generales.estadoActivoId));
  protected readonly opcionesArea = computed(() => this.opciones(this.areas.value(), this.valor().generales.areaId));
  protected readonly opcionesProcesador = computed(() =>
    this.opciones(this.procesadores.value(), this.valor().tecnicos.procesadorId),
  );

  /** Generaciones de la familia del procesador elegido (D44); vacía sin procesador o si la familia no tiene. */
  protected readonly generaciones = computed(() => {
    const id = this.valor().tecnicos.procesadorId;
    const familia = this.procesadores.value()?.find((p) => p.id === id)?.generaciones ?? 'Ninguna';
    return id ? (this.listas.value()?.generacionesProcesador.find((g) => g.familia === familia)?.opciones ?? []) : [];
  });

  protected readonly opcionesAntivirus = computed(() =>
    this.opciones(this.antivirus.value(), this.valor().tecnicos.antivirusId),
  );

  protected readonly opcionesSistema = computed(() =>
    this.opciones(this.sistemas.value(), this.valor().tecnicos.sistemaOperativoId),
  );

  protected readonly cargando = computed(
    () =>
      this.listas.isLoading() ||
      this.tipos.isLoading() ||
      (this.esEdicion() && !this.cargada()) ||
      (this.esEdicion() && this.puedeFotos && !this.fotosCargadas()),
  );

  protected readonly errorCarga = computed(() => {
    const error = this.fichaActual.error() ?? this.listas.error() ?? this.tipos.error() ?? this.fotosActuales.error();
    if (!error) {
      return null;
    }
    const e = aErrorApi(error);
    return e.estado === 404 ? 'Este activo no existe o pertenece a otra red.' : e.mensaje;
  });

  /** Red donde buscar el establecimiento: al editar, la del activo (no cambia de red); al registrar, la elegida. */
  protected readonly sedeDeBusqueda = computed(() => {
    const ficha = this.fichaActual.value();
    if (this.esEdicion()) {
      return ficha?.sedeId ?? null;
    }
    return this.sesion.esSuperAdmin() ? this.sedeSeleccionada.sedeId() : null;
  });

  // ---------- Ficha técnica del tipo elegido (D43) ----------

  /** Nula hasta elegir el tipo. */
  protected readonly ficha = computed<Ficha | null>(() => {
    const id = this.valor().generales.tipoActivoId;
    if (!id) {
      return null;
    }
    return this.tipos.value()?.find((t) => t.id === id)?.ficha ?? 'Ninguna';
  });

  protected readonly nombreTipo = computed(
    () => this.tipos.value()?.find((t) => t.id === this.valor().generales.tipoActivoId)?.nombre ?? '',
  );

  protected readonly seDescartanDatos = computed(() => {
    const ficha = this.ficha();
    return ficha !== null && hayDatosQueSeDescartan(this.valor(), ficha);
  });

  // ---------- Cálculos y avisos ----------

  protected readonly renovacionPrevista = computed(() => {
    const a = this.valor().adquisicion;
    const fecha = fechaRenovacion(a.fechaAdquisicion || null, a.vidaUtilAnios);
    return fecha ? formatoFecha(fecha) : null;
  });

  protected readonly totalDiscos = computed(() => totalDiscosGb(this.valor().discos));
  protected readonly totalRam = computed(() => totalRamGb(this.valor().modulosRam));

  protected readonly capacidadesDisco = computed(() => this.numeros(this.listas.value()?.capacidadesDiscoGb));
  protected readonly capacidadesRam = computed(() => this.numeros(this.listas.value()?.capacidadesRamGb));
  protected readonly pantallas = computed(() => this.numeros(this.listas.value()?.pantallasPulgadas));
  protected readonly esDedicada = computed(() => this.valor().tecnicos.tipoGrafica === 'Dedicada');
  protected readonly esOtraPantalla = computed(() => this.valor().tecnicos.pantalla === 'otra');
  protected readonly capacidadMaximaDisco = computed(() => aNumero(this.listas.value()?.capacidadMaximaDiscoGb) ?? 100_000);

  /** Activos de la red que ya usan el número de serie escrito (no es un error: puede ser el mismo equipo). */
  protected readonly serieRepetida = toSignal(
    this.generales.numeroSerie.valueChanges.pipe(
      map((serie) => serie.trim()),
      debounceTime(ESPERA_SERIE_MS),
      distinctUntilChanged(),
      switchMap((numero) =>
        numero.length < 3
          ? of<ActivoConSerieDto[]>([])
          : from(this.api.invoke(buscarSerieActivo, { numero, ...(this.id() ? { excluirId: this.id() } : {}) })).pipe(
              catchError(() => of<ActivoConSerieDto[]>([])),
            ),
      ),
    ),
    { initialValue: [] as ActivoConSerieDto[] },
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => this.fotos.liberar());

    // Al editar: las fotos guardadas, una sola vez.
    effect(() => {
      const fotos = this.fotosActuales.value();
      const id = this.id();
      if (!fotos || !id || untracked(this.fotosCargadas)) {
        return;
      }
      untracked(() => {
        this.fotos.cargar(id, fotos);
        this.fotosCargadas.set(true);
      });
    });

    // Al editar: vuelca la ficha en el formulario cuando llegan la ficha y las listas.
    effect(() => {
      const ficha = this.fichaActual.value();
      const listas = this.listas.value();
      if (!ficha || !listas || untracked(this.cargada)) {
        return;
      }
      untracked(() => {
        const valor = formularioDesdeFicha(
          ficha,
          this.numeros(listas.capacidadesDiscoGb),
          this.numeros(listas.pantallasPulgadas),
        );
        this.discos.clear({ emitEvent: false });
        valor.discos.forEach((d) => this.discos.push(this.grupoDisco(d), { emitEvent: false }));
        this.modulosRam.clear({ emitEvent: false });
        valor.modulosRam.forEach((m) => this.modulosRam.push(this.grupoModulo(m), { emitEvent: false }));
        this.formulario.patchValue(valor);
        this.alCambiarPantalla();
        const e = ficha.establecimiento;
        this.establecimiento.set({
          id: e.id,
          sedeId: ficha.sedeId,
          codigoUnico: e.codigoUnico,
          nombre: e.nombre,
          detalle: [e.categoria ? `Categoría ${e.categoria}` : null, e.microred, [e.distrito, e.provincia].filter((x) => !!x).join(', ')]
            .filter((x) => !!x)
            .join(' · '),
        });
        this.formulario.markAsPristine();
        this.cargada.set(true);
      });
    });

    // Las secciones que la ficha no lleva se desactivan: no se validan ni se envían.
    effect(() => {
      const ficha = this.ficha();
      untracked(() => this.ajustarSecciones(ficha));
    });

    // El área depende de la red del establecimiento: sin establecimiento no se puede elegir.
    effect(() => {
      const hay = this.establecimiento() !== null;
      untracked(() => (hay ? this.generales.areaId.enable() : this.generales.areaId.disable()));
    });

    // Una generación que no es de la familia del procesador elegido se quita (al cambiar de procesador, o si el
    // superadministrador cambió las generaciones de la familia de un activo ya registrado).
    effect(() => {
      const opciones = this.generaciones();
      if (!this.procesadores.hasValue() || !this.listas.hasValue()) {
        return;
      }
      untracked(() => {
        const generacion = this.tecnicos.generacionProcesador;
        if (generacion.value !== null && !opciones.some((o) => o.valor === generacion.value)) {
          generacion.setValue(null);
        }
      });
    });

    // Al cambiar la fecha de adquisición se vuelve a revisar el fin de garantía.
    this.adquisicion.fechaAdquisicion.valueChanges.subscribe(() =>
      this.adquisicion.finGarantia.updateValueAndValidity({ emitEvent: false }),
    );
  }

  // ---------- Discos y RAM ----------

  protected agregarDisco(): void {
    const primero = this.discos.length === 0;
    this.discos.push(
      this.grupoDisco({
        ranura: '',
        tipo: 'SsdSata',
        capacidad: null,
        capacidadOtra: null,
        marcaModelo: '',
        numeroSerie: '',
        uso: primero ? 'Sistema' : 'Datos',
      }),
    );
    this.formulario.markAsDirty();
  }

  protected quitarDisco(indice: number): void {
    this.discos.removeAt(indice);
    this.formulario.markAsDirty();
  }

  protected agregarModulo(): void {
    // El nuevo módulo copia tipo y velocidad del anterior: casi siempre son iguales.
    const anterior = this.modulosRam.length > 0 ? (this.modulosRam.at(-1).getRawValue() as ValorModuloRam) : null;
    this.modulosRam.push(
      this.grupoModulo({
        ranura: '',
        tipo: anterior?.tipo ?? 'Ddr4',
        capacidadGb: anterior?.capacidadGb ?? null,
        velocidadMhz: anterior?.velocidadMhz ?? null,
        marcaModelo: anterior?.marcaModelo ?? '',
      }),
    );
    this.formulario.markAsDirty();
  }

  protected quitarModulo(indice: number): void {
    this.modulosRam.removeAt(indice);
    this.formulario.markAsDirty();
  }

  /** Velocidades que corresponden al tipo de RAM (D41). */
  protected velocidadesDe(tipo: string): number[] {
    return this.numeros(this.listas.value()?.velocidadesRam.find((v) => v.tipo === tipo)?.velocidadesMhz);
  }

  /** Al cambiar el tipo de RAM, una velocidad que no es de ese tipo se quita. */
  protected alCambiarTipoRam(grupo: FormGroup): void {
    const velocidad = grupo.controls['velocidadMhz'];
    if (velocidad.value !== null && !this.velocidadesDe(grupo.controls['tipo'].value as string).includes(velocidad.value as number)) {
      velocidad.setValue(null);
    }
  }

  protected esOtraCapacidad(grupo: FormGroup): boolean {
    return grupo.controls['capacidad'].value === 'otra';
  }

  /** "Otra" capacidad: el campo para escribirla solo es obligatorio cuando se elige. */
  protected alCambiarCapacidad(grupo: FormGroup): void {
    const otra = grupo.controls['capacidadOtra'];
    if (this.esOtraCapacidad(grupo)) {
      otra.setValidators([Validators.required, Validators.min(1), Validators.max(this.capacidadMaximaDisco()), entero]);
    } else {
      otra.clearValidators();
      otra.setValue(null);
    }
    otra.updateValueAndValidity();
  }

  /** Pantalla "Otra…": el campo para escribir las pulgadas solo es obligatorio cuando se elige. */
  protected alCambiarPantalla(): void {
    const otra = this.tecnicos.pantallaOtra;
    if (this.tecnicos.pantalla.value === 'otra') {
      otra.setValidators([Validators.required, Validators.min(1), Validators.max(150), decimales(1)]);
    } else {
      otra.clearValidators();
      otra.setValue(null, { emitEvent: false });
    }
    otra.updateValueAndValidity();
  }

  protected alElegirEstablecimiento(elegido: EstablecimientoElegido | null): void {
    const anterior = this.establecimiento();
    this.establecimiento.set(elegido);
    this.formulario.markAsDirty();
    // Otra red (superadministrador): el área elegida es de la red anterior.
    if (elegido && anterior && elegido.sedeId !== anterior.sedeId) {
      this.generales.areaId.setValue(null);
    }
  }

  /** Suma a la lista un elemento recién agregado al catálogo (queda elegido en el campo). */
  protected sumar(lista: ResourceRef<ElementoCatalogoDto[] | undefined>, nuevo: ElementoCatalogoDto): void {
    lista.update((elementos) =>
      [...(elementos ?? []).filter((e) => e.id !== nuevo.id), nuevo].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')),
    );
  }

  // ---------- Guardar y salir ----------

  protected async guardar(): Promise<void> {
    this.intentoGuardar.set(true);
    this.errorGuardar.set(null);
    const establecimiento = this.establecimiento();
    const ficha = this.ficha();

    if (this.formulario.invalid || !establecimiento || !ficha) {
      this.formulario.markAllAsTouched();
      this.enfocarPrimerError();
      return;
    }

    this.guardando.set(true);
    this.errorFotos.set(null);
    try {
      const cuerpo = fichaDesdeFormulario(this.valorActual(), establecimiento.id, ficha);
      const id = this.idActivo();
      let destino: string;
      if (id) {
        await this.api.invoke(editarActivo, { id, body: cuerpo });
        destino = id;
      } else {
        destino = (await this.api.invoke(crearActivo, { body: cuerpo })).id;
        this.idCreado.set(destino);
      }
      this.formulario.markAsPristine();

      // Las fotos van después de la ficha: si alguna falla, la ficha ya quedó guardada y solo se reintenta lo que falta.
      const errorFotos = this.puedeFotos ? await this.fotos.aplicar(destino) : null;
      if (errorFotos) {
        this.errorFotos.set(errorFotos);
        this.elemento.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
        return;
      }

      this.guardado = true;
      this.notificaciones.exito(
        this.id() ? 'Cambios guardados.' : `Se registró «${cuerpo.generales.codigoPatrimonial.toUpperCase()}».`,
      );
      await this.router.navigate(['/inventario', destino], { replaceUrl: !this.id() });
    } catch (error: unknown) {
      const e = aErrorApi(error);
      if (e.codigo === 'Activos.CodigoDuplicado') {
        this.generales.codigoPatrimonial.setErrors({ duplicado: true });
        this.generales.codigoPatrimonial.markAsTouched();
      }
      this.errorGuardar.set(e);
      this.elemento.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } finally {
      this.guardando.set(false);
    }
  }

  /** A dónde lleva Cancelar: la ficha al editar, la lista al registrar. */
  protected rutaCancelar(): string[] {
    const id = this.idActivo();
    return id ? ['/inventario', id] : ['/inventario'];
  }

  async puedeSalir(): Promise<boolean> {
    if (this.guardado || !this.hayCambios()) {
      return true;
    }
    return confirmar(this.dialogos, {
      titulo: 'Salir sin guardar',
      mensaje: this.formulario.dirty
        ? 'Hay cambios en el formulario que todavía no se guardaron. Si sale ahora, se perderán.'
        : 'Hay cambios en las fotos que todavía no se guardaron. Si sale ahora, se perderán.',
      confirmar: 'Salir sin guardar',
      peligro: true,
    });
  }

  /** Recargar o cerrar la pestaña con cambios: el navegador muestra su propia pregunta. */
  protected alCerrarLaPestana(evento: BeforeUnloadEvent): void {
    if (!this.guardado && this.hayCambios()) {
      evento.preventDefault();
    }
  }

  private hayCambios(): boolean {
    return this.formulario.dirty || (this.puedeFotos && this.fotos.hayCambios());
  }

  // ---------- Auxiliares ----------

  private catalogo(tipo: string) {
    return resource({ loader: () => this.api.invoke(listarElementosCatalogo, { tipo, incluirInactivos: true }) });
  }

  private opciones(lista: ElementoCatalogoDto[] | undefined, elegido: string | null): ElementoCatalogoDto[] {
    return opcionesDeCatalogo(lista ?? [], elegido);
  }

  private numeros(lista: readonly (number | string)[] | undefined): number[] {
    return (lista ?? []).map((x) => aNumero(x)).filter((x): x is number => x !== null);
  }

  private valorActual(): ValorFormularioActivo {
    return this.formulario.getRawValue() as unknown as ValorFormularioActivo;
  }

  private grupoDisco(d: ValorDisco): FormGroup {
    const grupo = this.fb.group({
      ranura: [d.ranura, Validators.maxLength(20)],
      tipo: [d.tipo, Validators.required],
      capacidad: new FormControl<CapacidadDisco>(d.capacidad, Validators.required),
      capacidadOtra: new FormControl<number | null>(d.capacidadOtra),
      marcaModelo: [d.marcaModelo, Validators.maxLength(100)],
      numeroSerie: [d.numeroSerie, Validators.maxLength(60)],
      uso: [d.uso, Validators.required],
    });
    if (d.capacidad === 'otra') {
      this.alCambiarCapacidad(grupo);
    }
    return grupo;
  }

  private grupoModulo(m: ValorModuloRam): FormGroup {
    return this.fb.group({
      ranura: [m.ranura, Validators.maxLength(20)],
      tipo: [m.tipo, Validators.required],
      capacidadGb: new FormControl<number | null>(m.capacidadGb, Validators.required),
      velocidadMhz: new FormControl<number | null>(m.velocidadMhz),
      marcaModelo: [m.marcaModelo, Validators.maxLength(100)],
    });
  }

  private ajustarSecciones(ficha: Ficha | null): void {
    const opciones = { emitEvent: false };
    const tecnicos = this.formulario.controls.tecnicos;
    for (const [nombre, control] of Object.entries(tecnicos.controls)) {
      const visible = ficha === 'Computo' || (ficha === 'Red' && (CAMPOS_DE_RED as readonly string[]).includes(nombre));
      if (visible) {
        control.enable(opciones);
      } else {
        control.disable(opciones);
      }
    }
    for (const lista of [this.discos, this.modulosRam]) {
      if (ficha === 'Computo') {
        lista.enable(opciones);
      } else {
        lista.disable(opciones);
      }
    }
    this.formulario.updateValueAndValidity();
  }

  /** Lleva al primer campo con error (o al buscador del establecimiento, si falta). */
  private enfocarPrimerError(): void {
    setTimeout(() => {
      const raiz = this.elemento.nativeElement;
      const campo =
        (!this.establecimiento() ? raiz.querySelector<HTMLElement>('app-buscador-establecimiento input') : null) ??
        raiz.querySelector<HTMLElement>('[aria-invalid="true"]');
      campo?.focus();
      campo?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }
}
