import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { fetchTorrentPeers } from '@/services/torrents.api';
import { formatSpeed } from '@/utils/formatters';
import { Pagination } from '@/components/Pagination';

type TorrentPeersTabProps = {
  torrentId: string;
};

export function TorrentPeersTab({ torrentId }: TorrentPeersTabProps) {
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 15;

  const { data: peers, isLoading } = useQuery({
    queryKey: ['torrentPeers', torrentId],
    queryFn: () => fetchTorrentPeers(torrentId),
    refetchInterval: 3000, // Poll every 3 seconds for live speeds
  });

  if (isLoading) {
    return <div className="p-4 text-center text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">Loading peers...</div>;
  }

  if (!peers || peers.length === 0) {
    return <div className="p-4 text-center text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">No peers connected.</div>;
  }

  const totalPages = Math.ceil(peers.length / itemsPerPage);
  const paginatedPeers = peers.slice((currentPage - 1) * itemsPerPage, currentPage * itemsPerPage);

  return (
    <div className="flex flex-col">
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm text-[#4B4A47] dark:text-[#E9E9E7]">
          <thead className="bg-[#f9fafb] text-[12px] uppercase text-[#5F5E5B] dark:bg-[#1a1a1a] dark:text-[#C4C4C4]">
            <tr>
              <th className="px-4 py-3 font-medium">IP Address</th>
              <th className="px-4 py-3 font-medium">Client</th>
              <th className="px-4 py-3 font-medium">Down Speed</th>
              <th className="px-4 py-3 font-medium">Up Speed</th>
              <th className="px-4 py-3 font-medium text-center">Flags</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#e5e7eb] dark:divide-[#333333]">
            {paginatedPeers.map((peer, idx) => (
              <tr key={idx} className="transition-colors hover:bg-[#f9fafb] dark:hover:bg-[#1a1a1a]">
                <td className="whitespace-nowrap px-4 py-3 font-medium">{peer.connectionUri}</td>
                <td className="whitespace-nowrap px-4 py-3">{peer.clientApp}</td>
                <td className="whitespace-nowrap px-4 py-3 text-[#22c55e]">{formatSpeed(peer.downloadSpeedBytesPerSecond)}</td>
                <td className="whitespace-nowrap px-4 py-3 text-[#2563eb]">{formatSpeed(peer.uploadSpeedBytesPerSecond)}</td>
                <td className="whitespace-nowrap px-4 py-3 text-center tracking-widest text-[11px] font-mono">
                  <span title="Choking (Client)" className={peer.isAmChoking ? 'text-red-500' : 'text-gray-300 dark:text-gray-600'}>C</span>
                  <span title="Interested (Client)" className={peer.isAmInterested ? 'text-green-500' : 'text-gray-300 dark:text-gray-600'}>I</span>
                  <span className="mx-1">|</span>
                  <span title="Choking (Peer)" className={peer.isPeerChoking ? 'text-red-500' : 'text-gray-300 dark:text-gray-600'}>c</span>
                  <span title="Interested (Peer)" className={peer.isPeerInterested ? 'text-green-500' : 'text-gray-300 dark:text-gray-600'}>i</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {peers.length > itemsPerPage && (
        <Pagination
          currentPage={currentPage}
          totalPages={totalPages}
          onPageChange={setCurrentPage}
        />
      )}
    </div>
  );
}
