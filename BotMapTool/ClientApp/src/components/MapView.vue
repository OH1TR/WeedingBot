<script setup>
import { onMounted, onUnmounted, ref } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { HubConnectionBuilder } from '@microsoft/signalr'

const mapEl = ref(null)
const showOrto = ref(false)
const botStatus = ref('Ei yhteyttä palvelimeen')

let map = null
let maastokartta = null
let ortokuva = null
let hubConnection = null
let botMarker = null
let firstFix = true

// Tile requests go through our own proxy (/mml/...), which adds the MML API key.
const mmlLayer = (layer) => L.tileLayer(
  `/mml/wmts/1.0.0/${layer}/default/WGS84_Pseudo-Mercator/{z}/{y}/{x}.png`, {
    maxZoom: 18,
    attribution: '&copy; Maanmittauslaitos'
  })

onMounted(async () => {
  map = L.map(mapEl.value).setView([61.5, 24.0], 6)
  maastokartta = mmlLayer('maastokartta')
  ortokuva = mmlLayer('ortokuva')
  maastokartta.addTo(map)

  await connectHub()
})

onUnmounted(() => {
  hubConnection?.stop()
  map?.remove()
})

// WebSocket push from our own backend; the backend handles the MQTT connection.
async function connectHub() {
  hubConnection = new HubConnectionBuilder()
    .withUrl('/bothub')
    .withAutomaticReconnect()
    .build()

  hubConnection.on('botLocation', updateBotPosition)

  hubConnection.onreconnecting(() => {
    botStatus.value = 'Yhdistetään uudelleen…'
  })

  hubConnection.onreconnected(() => {
    botStatus.value = 'Odotetaan paikkatietoa…'
  })

  hubConnection.onclose(() => {
    botStatus.value = 'Ei yhteyttä palvelimeen'
  })

  try {
    await hubConnection.start()
    botStatus.value = 'Odotetaan paikkatietoa…'
  } catch {
    botStatus.value = 'Ei yhteyttä palvelimeen'
  }
}

// Position is WGS84 from BotControl: { latitude, longitude, speed, heading, satellites, ... }
function updateBotPosition(pos) {
  const latLng = [pos.latitude, pos.longitude]

  if (!botMarker) {
    botMarker = L.circleMarker(latLng, {
      radius: 8,
      color: '#fff',
      weight: 2,
      fillColor: '#d32f2f',
      fillOpacity: 1
    }).addTo(map)
  } else {
    botMarker.setLatLng(latLng)
  }

  if (firstFix) {
    firstFix = false
    map.setView(latLng, 16)
  }

  const speed = pos.speed != null ? `${pos.speed.toFixed(1)} m/s` : '–'
  botStatus.value = `Botti: ${pos.latitude.toFixed(6)}, ${pos.longitude.toFixed(6)} | ${speed} | sat ${pos.satellites}`
}

// Toggle between the default map and the orthophoto.
function toggleLayer() {
  showOrto.value = !showOrto.value
  map.removeLayer(showOrto.value ? maastokartta : ortokuva)
  map.addLayer(showOrto.value ? ortokuva : maastokartta)
}
</script>

<template>
  <div class="map-wrap">
    <div ref="mapEl" class="map"></div>
    <button class="layer-toggle" @click="toggleLayer">
      {{ showOrto ? 'Maastokartta' : 'Ortokuva' }}
    </button>
    <div class="bot-status">{{ botStatus }}</div>
  </div>
</template>

<style scoped>
.map-wrap {
  position: relative;
  height: 100%;
}

.map {
  height: 100%;
}

.layer-toggle {
  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 1000;
  background: #fff;
  border: 2px solid rgba(0, 0, 0, 0.2);
  border-radius: 4px;
  padding: 6px 10px;
  font: 14px/1.4 sans-serif;
  cursor: pointer;
}

.layer-toggle:hover {
  background: #f4f4f4;
}

.bot-status {
  position: absolute;
  bottom: 10px;
  left: 10px;
  z-index: 1000;
  background: rgba(255, 255, 255, 0.9);
  border-radius: 4px;
  padding: 4px 8px;
  font: 13px/1.4 sans-serif;
}
</style>
